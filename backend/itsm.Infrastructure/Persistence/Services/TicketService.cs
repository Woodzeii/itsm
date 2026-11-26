using System.Text.Json;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Persistence.Services;

public sealed class TicketService(ItsmDbContext db, ITicketAttachmentStorage attachmentStorage) : ITicketService
{
    private INotificationService? notificationService;

    public TicketService(
        ItsmDbContext db,
        ITicketAttachmentStorage attachmentStorage,
        INotificationService notificationService)
        : this(db, attachmentStorage)
    {
        this.notificationService = notificationService;
    }

    private const string RepairTypeCode = "repair";
    private const string ClosedStatusCode = "closed";
    private const string PendingStatusCode = "pending";
    private const string ReviewStatusCode = "review";
    private const string EscalatedStatusCode = "escalated";
    private const string InProgressStatusCode = "in_progress";
    private const long MaxAttachmentSizeBytes = 10 * 1024 * 1024;

    public async Task<IReadOnlyList<TicketResponse>> GetQueueAsync(CancellationToken ct = default, string? search = null)
    {
        var query = TicketQuery();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(ticket =>
                EF.Functions.ILike(ticket.TicketNumber, term)
                || EF.Functions.ILike(ticket.Title, term));
        }

        var tickets = await query
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ToListAsync(ct);

        return tickets.Select(ToResponse).ToArray();
    }

    public async Task<IReadOnlyList<TicketResponse>> GetMyTicketsAsync(int userId, CancellationToken ct = default)
    {
        var tickets = await TicketQuery()
            .Where(ticket => ticket.CreatorId == userId || ticket.RequesterId == userId)
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ToListAsync(ct);

        return tickets.Select(ToResponse).ToArray();
    }

    public async Task<IReadOnlyList<PortalTicketTypeResponse>> GetPortalTicketTypesAsync(CancellationToken ct = default) =>
        await db.TicketTypes
            .AsNoTracking()
            .Where(type => type.IsPortalAvailable && !type.IsArchived && type.Code != RepairTypeCode)
            .OrderBy(type => type.Id)
            .Select(type => new PortalTicketTypeResponse(type.Id, type.Name))
            .ToArrayAsync(ct);

    public async Task<TicketLookupsResponse> GetLookupsAsync(CancellationToken ct = default)
    {
        var types = await db.TicketTypes
            .AsNoTracking()
            .Where(type => !type.IsArchived)
            .OrderBy(type => type.Id)
            .Select(type => new TicketTypeLookup(type.Id, type.Code ?? string.Empty, type.Name))
            .ToArrayAsync(ct);
        var statuses = await db.TicketStatuses
            .AsNoTracking()
            .OrderBy(status => status.SortOrder)
            .ThenBy(status => status.Id)
            .Select(status => new TicketStatusLookup(status.Id, status.Code ?? string.Empty, status.Name))
            .ToArrayAsync(ct);
        return new TicketLookupsResponse(types, statuses);
    }

    public async Task<IReadOnlyList<RequesterOptionResponse>> GetRequesterOptionsAsync(CancellationToken ct = default) =>
        await db.Users
            .AsNoTracking()
            .Where(user => user.IsActive)
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Username)
            .Select(user => new RequesterOptionResponse(
                user.Id,
                user.Username,
                user.FullName,
                user.Department,
                user.Email))
            .ToArrayAsync(ct);

    public async Task<TicketResponse?> GetByIdAsync(
        int id,
        int userId,
        bool isStaff,
        CancellationToken ct = default)
    {
        var ticket = await TicketQuery()
            .FirstOrDefaultAsync(ticket => ticket.Id == id, ct);

        if (ticket is null || (!isStaff && ticket.CreatorId != userId && ticket.RequesterId != userId))
            return null;

        return ToResponse(ticket);
    }

    public async Task<TicketResponse> CreateAsync(
        CreateTicketRequest request,
        int creatorId,
        bool isStaff,
        bool isEngineer,
        CancellationToken ct = default)
    {
        var type = await db.TicketTypes.FirstOrDefaultAsync(type => type.Id == request.TypeId && !type.IsArchived, ct)
            ?? throw new ArgumentException("Тип заявки не найден.");

        if (!isStaff && (!type.IsPortalAvailable || type.Code == RepairTypeCode))
            throw new UnauthorizedAccessException("Создание заявок этого типа недоступно пользователю портала.");

        var formTemplate = await db.FormTemplates
            .AsNoTracking()
            .Where(item => item.TenantId == type.TenantId && item.TicketTypeId == type.Id)
            .OrderByDescending(item => item.UpdatedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(ct);
        if (formTemplate is not null
            && TicketFormInputValidator.Validate(formTemplate.FieldsSchema, request, isStaff) is { } validationError)
            throw new ArgumentException(validationError);

        Asset? asset = null;
        if (request.AssetId is int assetId)
        {
            asset = await db.Assets.FirstOrDefaultAsync(item => item.Id == assetId, ct)
                ?? throw new ArgumentException("Актив не найден.");
        }

        var isRepair = type.Code == RepairTypeCode;
        if (isRepair && !isEngineer)
            throw new UnauthorizedAccessException("Создавать заявки на ремонт может только инженер технической поддержки.");
        if (isRepair && request.AssetId is null)
            throw new ArgumentException("Для заявки на ремонт необходимо указать актив.");

        if (isRepair && asset!.LifecycleStage is not (AssetLifecycleStages.InStock or AssetLifecycleStages.InUse))
            throw new InvalidOperationException("Заявку на ремонт можно создать только для актива на складе или в эксплуатации.");
        if (isRepair && asset!.LifecycleStage == AssetLifecycleStages.InStock && asset.WarehouseId is null)
            throw new InvalidOperationException("Для заявки на ремонт актив на складе должен быть привязан к складу.");
        if (isRepair && asset!.LifecycleStage == AssetLifecycleStages.InUse
            && (asset.AssignedUserId is not int assignedUserId
                || !await db.Users.AnyAsync(user => user.Id == assignedUserId && user.IsActive, ct)))
            throw new InvalidOperationException("Для заявки на ремонт актив в эксплуатации должен быть закреплен за активным сотрудником.");

        var initialStatusId = await db.TicketStatuses
            .Where(status => status.Code == "new")
            .Select(status => (int?)status.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Не настроен начальный статус заявок.");

        var requesterId = creatorId;
        if (request.RequesterId is int requestedRequesterId)
        {
            if (!isStaff)
                throw new UnauthorizedAccessException("Указывать заявителя может только сотрудник службы поддержки.");

            var requester = await db.Users
                .Where(user => user.Id == requestedRequesterId && user.IsActive)
                .Select(user => new { user.Id })
                .FirstOrDefaultAsync(ct)
                ?? throw new ArgumentException("Заявитель не найден.");

            requesterId = requester.Id;
        }

        var now = DateTimeOffset.UtcNow;
        var tenantId = db.GetCurrentTenantId();
        var ticketNumber = await GenerateTicketNumberAsync(tenantId, ct);
        var priority = request.Priority.Trim();
        var normalizedPriority = priority.ToLowerInvariant();
        var criticality = await db.CriticalityLevels.FirstOrDefaultAsync(
            level => level.Code.ToLower() == normalizedPriority
                || level.Name.ToLower() == normalizedPriority,
            ct);

        var ticket = new Ticket
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            TypeId = type.Id,
            StatusId = initialStatusId,
            Priority = priority,
            CriticalityLevelId = criticality?.Id,
            CreatorId = creatorId,
            RequesterId = requesterId,
            TenantId = tenantId,
            TicketNumber = ticketNumber,
            AssetId = request.AssetId,
            CustomFieldsData = CloneObject(request.CustomFieldsData),
            CreatedAt = now,
            UpdatedAt = now
        };
        if (criticality is not null)
            await ConfigureSlaAsync(ticket, criticality.Id, now, ct);

        db.Tickets.Add(ticket);
        var assetHistory = new List<AssetHistoryLog>();

        if (isRepair)
        {
            var previousStage = asset!.LifecycleStage;
            asset.LifecycleStage = AssetLifecycleStages.InRepair;
            assetHistory.Add(new AssetHistoryLog
            {
                AssetId = asset.Id,
                UserId = creatorId,
                ActionType = AssetHistoryActionTypes.TicketCreated,
                Details = JsonSerializer.Serialize(new { ticketType = RepairTypeCode }),
                Comment = ticket.Title,
                ChangedAt = now
            });
            assetHistory.Add(new AssetHistoryLog
            {
                AssetId = asset.Id,
                UserId = creatorId,
                ActionType = AssetHistoryActionTypes.StageChanged,
                OldStatus = previousStage,
                NewStatus = asset.LifecycleStage,
                Details = JsonSerializer.Serialize(new { old = previousStage, @new = asset.LifecycleStage }),
                ChangedAt = now
            });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        foreach (var history in assetHistory)
            history.TicketId = ticket.Id;
        db.AssetHistoryLogs.AddRange(assetHistory);
        db.TicketAuditLogs.Add(new TicketAuditLog
        {
            TicketId = ticket.Id,
            UserId = creatorId,
            FieldName = "created",
            NewValue = ticket.StatusId.ToString(),
            ChangedAt = now
        });
        if (await IsAutoAssignmentEnabledAsync(ct)
            && ticket.Type.Code != RepairTypeCode
            && await PickLeastLoadedAgentAsync(type.AssignmentGroupId, null, ct) is { } autoAssignee)
        {
            ticket.AssigneeId = autoAssignee.UserId;
            ticket.AssigneeGroupId = type.AssignmentGroupId;
            db.TicketAuditLogs.Add(new TicketAuditLog
            {
                TicketId = ticket.Id,
                UserId = creatorId,
                FieldName = "assigneeId",
                OldValue = null,
                NewValue = autoAssignee.UserId.ToString(),
                ChangedAt = now
            });
            db.TicketAuditLogs.Add(new TicketAuditLog
            {
                TicketId = ticket.Id,
                UserId = creatorId,
                FieldName = "autoAssigned",
                NewValue = JsonSerializer.Serialize(new
                {
                    assigneeId = autoAssignee.UserId,
                    openTickets = autoAssignee.OpenTickets,
                    strategy = "least-loaded"
                }),
                ChangedAt = now
            });
        }

        if (requesterId != creatorId)
        {
            db.TicketAuditLogs.Add(new TicketAuditLog
            {
                TicketId = ticket.Id,
                UserId = creatorId,
                FieldName = "requesterId",
                OldValue = null,
                NewValue = requesterId.ToString(),
                ChangedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var savedTicket = await TicketQuery()
            .FirstAsync(item => item.Id == ticket.Id, ct);
        return ToResponse(savedTicket);
    }

    public async Task<TicketResponse?> UpdateAsync(
        int id,
        UpdateTicketRequest request,
        int actorId,
        CancellationToken ct = default)
    {
        var ticket = await TicketQuery().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (ticket is null)
            return null;

        if (request.Priority is not null)
        {
            var priority = request.Priority.Trim();
            if (priority.Length == 0)
                throw new ArgumentException("Приоритет не может быть пустым.");

            AddAudit(ticket, actorId, "priority", ticket.Priority, priority);
            ticket.Priority = priority;
        }

        if (request.AssigneeId.ValueKind != JsonValueKind.Undefined)
        {
            await EnsureManualAssignerAsync(actorId, ct);
            int? assigneeId = request.AssigneeId.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Number when request.AssigneeId.TryGetInt32(out var parsedId) => parsedId,
                _ => throw new ArgumentException("assigneeId должен быть целым числом или null.")
            };

            if (assigneeId is int userId)
                await ValidateActiveAgentAsync(userId, ct);

            AddAudit(ticket, actorId, "assigneeId", ticket.AssigneeId?.ToString(), assigneeId?.ToString());
            ticket.AssigneeId = assigneeId;
            ticket.AssigneeGroupId = null;
            ticket.Assignee = assigneeId is int assignedUserId
                ? await db.Users.FirstAsync(user => user.Id == assignedUserId, ct)
                : null;
        }

        if (request.CustomFieldsData is JsonElement fields)
        {
            if (fields.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("customFieldsData должен быть JSON-объектом.");

            AddAudit(
                ticket,
                actorId,
                "customFieldsData",
                ticket.CustomFieldsData?.GetRawText(),
                fields.GetRawText());
            ticket.CustomFieldsData = fields.Clone();
        }

        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        var updatedTicket = await TicketQuery().FirstAsync(item => item.Id == ticket.Id, ct);
        return ToResponse(updatedTicket);
    }

    public async Task<TicketResponse?> ChangeStatusAsync(
        int id,
        ChangeTicketStatusRequest request,
        int actorId,
        IReadOnlyCollection<string> roleCodes,
        CancellationToken ct = default)
    {
        var ticket = await TicketQuery().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (ticket is null)
            return null;

        var targetStatus = await db.TicketStatuses.FirstOrDefaultAsync(status => status.Id == request.StatusId, ct)
            ?? throw new ArgumentException("Статус заявки не найден.");
        if (ticket.StatusId == targetStatus.Id)
            throw new InvalidOperationException("Заявка уже находится в указанном статусе.");

        var transition = await db.TicketStatusTransitions
            .FirstOrDefaultAsync(item => item.FromStatusId == ticket.StatusId && item.ToStatusId == targetStatus.Id, ct);
        if (transition is null)
            throw new InvalidOperationException("Переход между указанными статусами не разрешен.");
        if (transition.RequiredRoleCode is not null && !roleCodes.Contains(transition.RequiredRoleCode, StringComparer.Ordinal))
            throw new UnauthorizedAccessException("Для перехода в этот статус недостаточно прав.");

        var isRepair = ticket.Type.Code == RepairTypeCode;
        if (targetStatus.Code == ClosedStatusCode && isRepair)
        {
            if (string.IsNullOrWhiteSpace(request.RepairResolution) || request.RepairOutcome is null)
                throw new ArgumentException("При закрытии заявки на ремонт необходимо указать результат и исход ремонта.");

            var asset = await db.Assets.FirstOrDefaultAsync(item => item.Id == ticket.AssetId, ct)
                ?? throw new InvalidOperationException("У заявки на ремонт отсутствует связанный актив.");
            if (asset.LifecycleStage != AssetLifecycleStages.InRepair)
                throw new InvalidOperationException("Закрыть заявку на ремонт можно только для актива на этапе «В ремонте».");

            string newStage;
            switch (request.RepairOutcome.Value)
            {
                case RepairOutcome.ReturnToExploitation:
                    if (request.RepairToWarehouseCode is not null)
                        throw new ArgumentException("Для возврата в эксплуатацию нельзя указывать склад назначения.");

                    var existingAssigneeIsActive = asset.AssignedUserId is int existingAssigneeId
                        && await db.Users.AnyAsync(user => user.Id == existingAssigneeId && user.IsActive, ct);
                    if (existingAssigneeIsActive && request.RepairToUserId is int requestedAssigneeId
                        && requestedAssigneeId != asset.AssignedUserId)
                        throw new ArgumentException("Для возврата в эксплуатацию будет сохранен текущий сотрудник актива.");

                    var exploitationAssigneeId = existingAssigneeIsActive
                        ? asset.AssignedUserId
                        : request.RepairToUserId;
                    if (exploitationAssigneeId is not int exploitationUserId)
                        throw new ArgumentException("Для возврата в эксплуатацию укажите сотрудника.");
                    if (!existingAssigneeIsActive
                        && !await db.Users.AnyAsync(user => user.Id == exploitationUserId && user.IsActive, ct))
                        throw new ArgumentException("Сотрудник для возврата актива не найден.");

                    asset.AssignedUserId = exploitationUserId;
                    asset.WarehouseId = null;
                    newStage = AssetLifecycleStages.InUse;
                    break;

                case RepairOutcome.TransferToWarehouse:
                    if (request.RepairToUserId is not null)
                        throw new ArgumentException("Для возврата на склад нельзя указывать сотрудника.");

                    int? requestedWarehouseId = null;
                    if (request.RepairToWarehouseCode is string requestedWarehouseCode)
                    {
                        requestedWarehouseId = await db.DictionaryValues
                            .Where(value => value.Code == requestedWarehouseCode
                                && !value.IsArchived
                                && !value.Dictionary.IsArchived
                                && value.Dictionary.TenantId == db.GetCurrentTenantId()
                                && value.Dictionary.Code == "warehouses")
                            .Select(value => (int?)value.Id)
                            .FirstOrDefaultAsync(ct);
                        if (requestedWarehouseId is null)
                            throw new FieldValidationException(
                                nameof(request.RepairToWarehouseCode),
                                $"Активный склад с кодом '{requestedWarehouseCode}' не найден.");
                    }

                    var existingWarehouseIsValid = asset.WarehouseId is int existingWarehouseId
                        && await db.DictionaryValues.AnyAsync(value =>
                            value.Id == existingWarehouseId
                            && !value.IsArchived
                            && !value.Dictionary.IsArchived
                            && value.Dictionary.TenantId == db.GetCurrentTenantId()
                            && value.Dictionary.Code == "warehouses", ct);
                    if (existingWarehouseIsValid && requestedWarehouseId is int requestedId
                        && requestedId != asset.WarehouseId)
                        throw new ArgumentException("Для возврата на склад будет сохранен текущий склад актива.");

                    var warehouseId = existingWarehouseIsValid
                        ? asset.WarehouseId
                        : requestedWarehouseId;
                    if (warehouseId is not int returnWarehouseId)
                        throw new FieldValidationException(
                            nameof(request.RepairToWarehouseCode), "Для возврата на склад укажите код склада.");

                    asset.WarehouseId = returnWarehouseId;
                    asset.AssignedUserId = null;
                    newStage = AssetLifecycleStages.InStock;
                    break;

                case RepairOutcome.Retire:
                    if (request.RepairToUserId is not null || request.RepairToWarehouseCode is not null)
                        throw new ArgumentException("Для списания нельзя указывать сотрудника или склад назначения.");

                    asset.AssignedUserId = null;
                    asset.WarehouseId = null;
                    newStage = AssetLifecycleStages.Decommissioned;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(request.RepairOutcome));
            }

            var oldStage = asset.LifecycleStage;
            asset.LifecycleStage = newStage;
            var assignedWarehouseCode = asset.WarehouseId is int assignedWarehouseId
                ? await db.DictionaryValues
                    .Where(value => value.Id == assignedWarehouseId
                        && value.Dictionary.TenantId == db.GetCurrentTenantId()
                        && value.Dictionary.Code == "warehouses")
                    .Select(value => value.Code)
                    .FirstOrDefaultAsync(ct)
                : null;
            db.AssetHistoryLogs.Add(new AssetHistoryLog
            {
                AssetId = asset.Id,
                UserId = actorId,
                TicketId = ticket.Id,
                ActionType = AssetHistoryActionTypes.TicketClosed,
                OldStatus = oldStage,
                NewStatus = newStage,
                Details = JsonSerializer.Serialize(new
                {
                    repairOutcome = request.RepairOutcome.Value.ToString(),
                    repairResolution = request.RepairResolution.Trim(),
                    assignedUserId = asset.AssignedUserId,
                    warehouseCode = assignedWarehouseCode
                }),
                Comment = request.RepairResolution.Trim(),
                ChangedAt = DateTimeOffset.UtcNow
            });
            db.TicketMessages.Add(new TicketMessage
            {
                TicketId = ticket.Id,
                AuthorId = actorId,
                MessageBody = request.RepairResolution.Trim(),
                IsInternal = false,
                SourceType = "system",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else if (request.RepairResolution is not null
            || request.RepairOutcome is not null
            || request.RepairToUserId is not null
            || request.RepairToWarehouseCode is not null)
        {
            throw new ArgumentException("Результат ремонта и назначения можно передавать только при закрытии заявки на ремонт.");
        }

        var oldStatusId = ticket.StatusId;
        var now = DateTimeOffset.UtcNow;
        RecordSlaBreaches(ticket, now);
        var wasSlaPaused = ticket.IsSlaPaused;
        if (!wasSlaPaused && targetStatus.IsSlaPausing)
        {
            ticket.SlaPausedAt = now;
        }
        else if (wasSlaPaused && !targetStatus.IsSlaPausing)
        {
            ResumeSlaTimers(ticket, now);
        }

        ticket.StatusId = targetStatus.Id;
        ticket.Status = targetStatus;
        ticket.IsSlaPaused = targetStatus.IsSlaPausing;
        ticket.SlaTimerPaused = targetStatus.IsSlaPausing;
        if (ticket.SlaPolicyId is not null && targetStatus.Code == PendingStatusCode)
            ticket.ActualReactionTime ??= now;
        if (targetStatus.Code is ReviewStatusCode or ClosedStatusCode)
            ticket.ActualResolutionTime ??= now;
        RecordSlaBreaches(ticket, now);
        ticket.UpdatedAt = now;
        AddAudit(ticket, actorId, "statusId", oldStatusId.ToString(), targetStatus.Id.ToString());

        await db.SaveChangesAsync(ct);
        await NotifyRecipientsAsync(
            ticket,
            actorId,
            "TicketStatusChanged",
            $"Статус заявки «{ticket.Title}» изменён на «{targetStatus.Name}».",
            ct);
        var updatedTicket = await TicketQuery().FirstAsync(item => item.Id == ticket.Id, ct);
        return ToResponse(updatedTicket);
    }

    public async Task<TicketResponse?> AssignAsync(
        int id,
        AssignTicketRequest request,
        int actorId,
        CancellationToken ct = default)
    {
        var ticket = await TicketQuery().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (ticket is null)
            return null;

        await EnsureManualAssignerAsync(actorId, ct);

        await ValidateActiveAgentAsync(request.AssigneeId, ct);
        var oldAssigneeId = ticket.AssigneeId;
        ticket.AssigneeId = request.AssigneeId;
        ticket.Assignee = await db.Users.FirstAsync(user => user.Id == request.AssigneeId, ct);
        ticket.AssigneeGroupId = null;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit(ticket, actorId, "assigneeId", oldAssigneeId?.ToString(), request.AssigneeId.ToString());

        await db.SaveChangesAsync(ct);
        if (request.AssigneeId != actorId)
        {
            await CreateNotificationAsync(
                request.AssigneeId,
                "TicketAssigned",
                $"Вам назначена заявка «{ticket.Title}».",
                ticket.Id,
                ct);
        }
        var updatedTicket = await TicketQuery().FirstAsync(item => item.Id == ticket.Id, ct);
        return ToResponse(updatedTicket);
    }

    public async Task<AutoAssignResponse?> AutoAssignAsync(int id, int actorId, CancellationToken ct = default)
    {
        var ticket = await TicketQuery().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (ticket is null)
            return null;

        if (ticket.Status.Code == ClosedStatusCode)
            throw new InvalidOperationException("Закрытую заявку нельзя перераспределить.");

        var groupId = ticket.Type.AssignmentGroupId;
        var candidate = await PickLeastLoadedAgentAsync(groupId, null, ct)
            ?? throw new InvalidOperationException("Нет доступных активных инженеров для назначения.");

        var oldAssigneeId = ticket.AssigneeId;
        if (oldAssigneeId == candidate.UserId && ticket.AssigneeGroupId == groupId)
            return new AutoAssignResponse(
                ticket.Id,
                candidate.UserId,
                candidate.FullName,
                candidate.OpenTickets,
                "Наименее загруженный инженер уже назначен на заявку.");

        ticket.AssigneeId = candidate.UserId;
        ticket.Assignee = await db.Users.FirstAsync(user => user.Id == candidate.UserId, ct);
        ticket.AssigneeGroupId = groupId;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit(ticket, actorId, "assigneeId", oldAssigneeId?.ToString(), candidate.UserId.ToString());
        AddAudit(
            ticket,
            actorId,
            "autoAssigned",
            oldAssigneeId?.ToString(),
            JsonSerializer.Serialize(new
            {
                assigneeId = candidate.UserId,
                openTickets = candidate.OpenTickets,
                strategy = "least-loaded"
            }));

        await db.SaveChangesAsync(ct);
        return new AutoAssignResponse(
            ticket.Id,
            candidate.UserId,
            candidate.FullName,
            candidate.OpenTickets,
            "Назначен наименее загруженный инженер.");
    }

    public async Task<AssignmentSettingResponse> GetAssignmentSettingAsync(CancellationToken ct = default)
    {
        var setting = await db.AssignmentSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == 1, ct);
        var manualAssignerName = setting?.ManualAssignerUserId is int manualAssignerId
            ? await db.Users.Where(user => user.Id == manualAssignerId).Select(user => user.FullName).FirstOrDefaultAsync(ct)
            : null;
        var activeAgents = await CountActiveAgentsAsync(ct);

        return new AssignmentSettingResponse(
            setting?.Mode ?? "auto",
            setting?.ManualAssignerUserId,
            manualAssignerName,
            activeAgents);
    }

    public async Task<AssignmentSettingResponse> UpdateAssignmentSettingAsync(
        UpdateAssignmentSettingRequest request,
        CancellationToken ct = default)
    {
        if (request.ManualAssignerUserId is int manualAssignerId && !await db.Users.AnyAsync(user =>
            user.Id == manualAssignerId && user.IsActive && user.RoleMappings.Any(mapping =>
                mapping.Role.Code == "admin" || mapping.Role.Code == "tenant_owner" || mapping.Role.Code == "manager"), ct))
            throw new ArgumentException("Ответственным руководителем может быть только активный администратор или менеджер.");

        var setting = await db.AssignmentSettings.FirstOrDefaultAsync(item => item.Id == 1, ct);
        if (setting is null)
        {
            setting = new AssignmentSetting { Id = 1, TenantId = db.CurrentTenantId, Mode = request.Mode, ManualAssignerUserId = request.ManualAssignerUserId };
            db.AssignmentSettings.Add(setting);
        }
        else
        {
            setting.Mode = request.Mode;
            setting.ManualAssignerUserId = request.ManualAssignerUserId;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return await GetAssignmentSettingAsync(ct);
    }

    private async Task EnsureManualAssignerAsync(int actorId, CancellationToken ct)
    {
        var requiredId = await db.AssignmentSettings.Where(item => item.Id == 1)
            .Select(item => item.ManualAssignerUserId).FirstOrDefaultAsync(ct);
        if (requiredId is int id && (actorId != id || !await db.Users.AnyAsync(user => user.Id == id && user.IsActive, ct)))
            throw new UnauthorizedAccessException("Ручное назначение разрешено только ответственному руководителю.");
    }

    private async Task<bool> IsAutoAssignmentEnabledAsync(CancellationToken ct)
    {
        var mode = await db.AssignmentSettings
            .Where(item => item.Id == 1)
            .Select(item => item.Mode)
            .FirstOrDefaultAsync(ct);

        return string.Equals(mode ?? "auto", "auto", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<int> CountActiveAgentsAsync(CancellationToken ct) =>
        await db.Users.CountAsync(user => user.IsActive
            && user.RoleMappings.Any(mapping => mapping.Role.Code == "agent"), ct);

    private sealed record AgentCandidate(int UserId, string FullName, int OpenTickets);

    private async Task<AgentCandidate?> PickLeastLoadedAgentAsync(int? groupId, int? excludeUserId, CancellationToken ct)
    {
        // Группа без участников не должна блокировать назначение — откатываемся на общий пул.
        if (groupId is int knownGroupId
            && !await db.AgentGroupMembers.AnyAsync(member => member.AgentGroupId == knownGroupId, ct))
        {
            groupId = null;
        }

        return await PickLeastLoadedFromPoolAsync(groupId, excludeUserId, ct);
    }

    private async Task<AgentCandidate?> PickLeastLoadedFromPoolAsync(int? groupId, int? excludeUserId, CancellationToken ct)
    {
        var closedStatusId = await db.TicketStatuses
            .Where(status => status.Code == ClosedStatusCode)
            .Select(status => (int?)status.Id)
            .FirstOrDefaultAsync(ct);

        return await db.Users
            .AsNoTracking()
            .Where(user => user.IsActive
                && user.RoleMappings.Any(mapping => mapping.Role.Code == "agent")
                && (user.LockedUntil == null || user.LockedUntil <= DateTimeOffset.UtcNow)
                && (groupId == null || db.AgentGroupMembers.Any(member => member.AgentGroupId == groupId && member.UserId == user.Id && member.AgentGroup.TenantId == db.CurrentTenantId))
                && (excludeUserId == null || user.Id != excludeUserId))
            .Select(user => new
            {
                user.Id,
                user.FullName,
                OpenTickets = user.AssignedTickets.Count(ticket => closedStatusId == null || ticket.StatusId != closedStatusId),
                LastAssignedAt = user.AssignedTickets
                    .Where(ticket => closedStatusId == null || ticket.StatusId != closedStatusId)
                    .OrderByDescending(ticket => ticket.CreatedAt)
                    .Select(ticket => (DateTimeOffset?)ticket.CreatedAt)
                    .FirstOrDefault()
            })
            .OrderBy(agent => agent.OpenTickets)
            .ThenBy(agent => agent.LastAssignedAt ?? DateTimeOffset.MinValue)
            .ThenBy(agent => agent.Id)
            .Select(agent => new AgentCandidate(agent.Id, agent.FullName, agent.OpenTickets))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<AssignmentHistoryItem>> GetAssignmentHistoryAsync(int? assigneeId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var query = db.TicketAuditLogs.AsNoTracking().Where(log => log.FieldName == "autoAssigned");
        if (from is DateTimeOffset start) query = query.Where(log => log.ChangedAt >= start);
        if (to is DateTimeOffset end) query = query.Where(log => log.ChangedAt <= end);
        var logs = await query
            .Include(log => log.Ticket)
            .Include(log => log.User)
            .OrderByDescending(log => log.ChangedAt)
            .ToListAsync(ct);

        var entries = logs
            .Select(log => new
            {
                Log = log,
                Payload = ParseAssignmentPayload(log.NewValue),
                PreviousAssigneeId = ParseAssigneeId(log.OldValue)
            })
            .ToArray();

        var userIds = entries
            .SelectMany(entry => new[] { entry.Payload.AssigneeId, entry.PreviousAssigneeId })
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var names = userIds.Length == 0
            ? new Dictionary<int, string>()
            : await db.Users
                .AsNoTracking()
                .Where(user => userIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.FullName, ct);

        return entries
            .Where(entry => assigneeId is null || entry.Payload.AssigneeId == assigneeId)
            .Select(entry => new AssignmentHistoryItem(
                entry.Log.TicketId,
                entry.Log.Ticket.TicketNumber,
                entry.Log.Ticket.Title,
                entry.PreviousAssigneeId,
                entry.PreviousAssigneeId is int previousId ? names.GetValueOrDefault(previousId) : null,
                entry.Payload.AssigneeId,
                entry.Payload.AssigneeId is int newId ? names.GetValueOrDefault(newId) : null,
                entry.Payload.Strategy,
                entry.Payload.OpenTickets,
                entry.Log.ChangedAt,
                entry.Log.User.FullName))
            .ToArray();
    }

    private static int? ParseAssigneeId(string? value) =>
        int.TryParse(value, out var parsedId) ? parsedId : null;

    private static AssignmentPayload ParseAssignmentPayload(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new AssignmentPayload(null, 0, "least-loaded");

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;

            int? assigneeId = root.TryGetProperty("assigneeId", out var assigneeProperty)
                && assigneeProperty.ValueKind == JsonValueKind.Number
                && assigneeProperty.TryGetInt32(out var parsedAssigneeId)
                    ? parsedAssigneeId
                    : null;

            var openTickets = root.TryGetProperty("openTickets", out var openTicketsProperty)
                && openTicketsProperty.ValueKind == JsonValueKind.Number
                && openTicketsProperty.TryGetInt32(out var parsedOpenTickets)
                    ? parsedOpenTickets
                    : 0;

            var strategy = root.TryGetProperty("strategy", out var strategyProperty)
                && strategyProperty.ValueKind == JsonValueKind.String
                    ? strategyProperty.GetString()
                    : null;
            strategy ??= root.TryGetProperty("reason", out var reasonProperty)
                && reasonProperty.ValueKind == JsonValueKind.String
                    ? reasonProperty.GetString()
                    : null;

            return new AssignmentPayload(assigneeId, openTickets, strategy ?? "least-loaded");
        }
        catch (JsonException)
        {
            return new AssignmentPayload(null, 0, "least-loaded");
        }
    }

    private readonly record struct AssignmentPayload(int? AssigneeId, int OpenTickets, string Strategy);

    public async Task<IReadOnlyList<AgentGroupOption>> GetAgentGroupsAsync(CancellationToken ct = default) =>
        await db.AgentGroups.AsNoTracking().OrderBy(group => group.Name).Select(group => new AgentGroupOption(group.Id, group.Code ?? "", group.Name, group.Members.Count)).ToArrayAsync(ct);

    public async Task<TicketResponse?> SetTicketAssignmentGroupAsync(int id, AssignmentGroupRequest request, int actorId, CancellationToken ct = default)
    {
        var ticket = await TicketQuery().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (ticket is null) return null;
        if (!await db.AgentGroups.AnyAsync(group => group.Id == request.GroupId, ct)) throw new ArgumentException("Группа исполнителей не найдена.");
        ticket.AssigneeGroupId = request.GroupId;
        ticket.AssigneeId = null;
        AddAudit(ticket, actorId, "assigneeGroupId", null, request.GroupId.ToString());
        await db.SaveChangesAsync(ct);
        return ToResponse(await TicketQuery().FirstAsync(item => item.Id == id, ct));
    }

    public async Task ReassignTicketsFromUserAsync(int userId, int actorId, CancellationToken ct = default)
    {
        var tickets = await db.Tickets.Where(ticket => ticket.AssigneeId == userId && ticket.Status.Code != ClosedStatusCode).ToListAsync(ct);
        foreach (var ticket in tickets)
        {
            var groupId = await db.TicketTypes.Where(type => type.Id == ticket.TypeId).Select(type => type.AssignmentGroupId).FirstOrDefaultAsync(ct);
            var candidate = await PickLeastLoadedAgentAsync(groupId, userId, ct);
            ticket.AssigneeId = candidate?.UserId;
            ticket.AssigneeGroupId = candidate is null ? ticket.AssigneeGroupId : groupId;
            AddAudit(ticket, actorId, "assigneeId", userId.ToString(), candidate?.UserId.ToString());
            AddAudit(ticket, actorId, "autoAssigned", null, JsonSerializer.Serialize(new
            {
                reason = "assignee-deactivated",
                assigneeId = candidate?.UserId,
                openTickets = candidate?.OpenTickets ?? 0,
                strategy = "assignee-deactivated"
            }));
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task ReassignUnavailableTicketsAsync(CancellationToken ct = default)
    {
        var unavailable = await db.Tickets
            .Where(ticket => ticket.AssigneeId != null && ticket.Assignee != null && !ticket.Assignee.IsActive && ticket.Status.Code != ClosedStatusCode)
            .Select(ticket => ticket.AssigneeId!.Value)
            .Distinct()
            .ToArrayAsync(ct);

        foreach (var userId in unavailable)
            await ReassignTicketsFromUserAsync(userId, userId, ct);
    }

    public async Task<TicketEscalationResponse?> EscalateAsync(
        int id,
        int actorId,
        CancellationToken ct = default)
    {
        var ticket = await TicketQuery().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (ticket is null)
            return null;

        if (ticket.CreatorId != actorId && ticket.AssigneeId != actorId)
            throw new UnauthorizedAccessException("Эскалировать заявку может только ее автор или исполнитель.");
        if (ticket.Status.Code == ClosedStatusCode)
            throw new InvalidOperationException("Закрытую заявку нельзя эскалировать.");
        if (ticket.Status.Code == EscalatedStatusCode)
            throw new InvalidOperationException("Заявка уже эскалирована и ожидает принятия.");

        var escalationRule = await db.EscalationRules
            .FirstOrDefaultAsync(rule => rule.TicketTypeId == ticket.TypeId, ct);
        if (escalationRule is not null && (!escalationRule.IsActive || !escalationRule.OnManual))
            throw new UnauthorizedAccessException("Ручная эскалация отключена для этого типа заявки.");

        var currentCriticality = ticket.CriticalityLevelId is int criticalityId
            ? await db.CriticalityLevels.FirstOrDefaultAsync(level => level.Id == criticalityId, ct)
            : null;
        var levels = await db.CriticalityLevels
            .Where(level => level.IsActive)
            .OrderBy(level => level.SortOrder)
            .ThenBy(level => level.Id)
            .ToListAsync(ct);
        var currentIndex = currentCriticality is null
            ? -1
            : levels.FindIndex(level => level.Id == currentCriticality.Id);
        CriticalityLevel? nextCriticality;
        if (currentCriticality is null)
        {
            nextCriticality = levels.FirstOrDefault();
        }
        else if (currentIndex >= 0)
        {
            nextCriticality = currentIndex + 1 < levels.Count ? levels[currentIndex + 1] : null;
        }
        else
        {
            nextCriticality = levels.FirstOrDefault(level => level.SortOrder > currentCriticality.SortOrder);
        }

        var headSetting = await db.ServiceDeskHeadSettings
            .Include(setting => setting.User)
            .FirstOrDefaultAsync(setting => setting.Id == 1, ct);
        if (headSetting is null || !headSetting.User.IsActive)
            throw new InvalidOperationException("Не настроен активный руководитель службы поддержки.");

        var isHeadAnAgent = await db.UserRoleMappings
            .AnyAsync(mapping => mapping.UserId == headSetting.UserId && mapping.Role.Code == "agent", ct);
        if (!isHeadAnAgent)
            throw new InvalidOperationException("Руководитель службы поддержки больше не является инженером.");

        var notifiedUserIds = await db.UserRoleMappings
            .Where(mapping => (mapping.Role.Code == "admin" || mapping.Role.Code == "tenant_owner")
                              && mapping.User.IsActive)
            .Select(mapping => mapping.UserId)
            .Distinct()
            .OrderBy(userId => userId)
            .ToListAsync(ct);
        if (!notifiedUserIds.Contains(headSetting.UserId))
            notifiedUserIds.Add(headSetting.UserId);

        var escalatedStatus = await db.TicketStatuses
            .FirstOrDefaultAsync(status => status.Code == EscalatedStatusCode, ct)
            ?? throw new InvalidOperationException("Не настроен статус «Эскалирована».");
        var oldStatusId = ticket.StatusId;

        var now = DateTimeOffset.UtcNow;
        var fromCriticalityId = ticket.CriticalityLevelId;
        var toCriticalityId = nextCriticality?.Id ?? fromCriticalityId;
        if (toCriticalityId != fromCriticalityId)
        {
            ticket.CriticalityLevelId = toCriticalityId;
            AddAudit(ticket, actorId, "criticalityLevelId", fromCriticalityId?.ToString(), toCriticalityId?.ToString());
        }

        ticket.StatusId = escalatedStatus.Id;
        ticket.Status = escalatedStatus;
        AddAudit(ticket, actorId, "statusId", oldStatusId.ToString(), escalatedStatus.Id.ToString());

        ticket.EscalationLevel++;
        ticket.UpdatedAt = now;
        var escalation = new TicketEscalation
        {
            TicketId = ticket.Id,
            InitiatorUserId = actorId,
            Reason = "OnManual",
            FromCriticalityLevelId = fromCriticalityId,
            ToCriticalityLevelId = toCriticalityId == fromCriticalityId ? null : toCriticalityId,
            NotifiedUserIds = JsonSerializer.Serialize(notifiedUserIds),
            EscalatedAt = now
        };
        db.TicketEscalations.Add(escalation);

        foreach (var recipientId in notifiedUserIds)
        {
            await CreateNotificationAsync(
                recipientId,
                "TicketEscalated",
                $"Заявка «{ticket.Title}» была эскалирована.",
                ticket.Id,
                ct);
        }

        await db.SaveChangesAsync(ct);
        return new TicketEscalationResponse(
            escalation.Id,
            escalation.TicketId,
            escalation.Ticket.TicketNumber,
            escalation.InitiatorUserId,
            escalation.Reason,
            escalation.FromCriticalityLevelId,
            escalation.ToCriticalityLevelId,
            notifiedUserIds,
            escalation.EscalatedAt,
            escalation.AcceptedByUserId,
            escalation.AcceptedAt,
            escalation.Comment);
    }

    public async Task<IReadOnlyList<TicketEscalationResponse>?> GetTicketEscalationsAsync(int id, CancellationToken ct = default)
    {
        if (!await db.Tickets.AnyAsync(ticket => ticket.Id == id, ct))
            return null;

        var escalations = await db.TicketEscalations
            .AsNoTracking()
            .Where(escalation => escalation.TicketId == id)
            .Include(escalation => escalation.Ticket)
            .OrderByDescending(escalation => escalation.EscalatedAt)
            .ThenByDescending(escalation => escalation.Id)
            .ToListAsync(ct);

        return escalations.Select(escalation => new TicketEscalationResponse(
            escalation.Id,
            escalation.TicketId,
            escalation.Ticket.TicketNumber,
            escalation.InitiatorUserId,
            escalation.Reason,
            escalation.FromCriticalityLevelId,
            escalation.ToCriticalityLevelId,
            ParseNotifiedUserIds(escalation.NotifiedUserIds),
            escalation.EscalatedAt,
            escalation.AcceptedByUserId,
            escalation.AcceptedAt,
            escalation.Comment)).ToArray();
    }

    public async Task<IReadOnlyList<PendingEscalationResponse>?> GetPendingEscalationsAsync(CancellationToken ct = default)
    {
        var escalatedStatusId = await db.TicketStatuses
            .Where(status => status.Code == EscalatedStatusCode)
            .Select(status => (int?)status.Id)
            .FirstOrDefaultAsync(ct);
        if (escalatedStatusId is null)
            return [];

        var pending = await db.TicketEscalations
            .AsNoTracking()
            .Where(escalation => escalation.AcceptedAt == null
                && escalation.Ticket.StatusId == escalatedStatusId)
            .Include(escalation => escalation.Ticket).ThenInclude(ticket => ticket.Type)
            .Include(escalation => escalation.Ticket).ThenInclude(ticket => ticket.Status)
            .Include(escalation => escalation.Ticket).ThenInclude(ticket => ticket.Assignee)
            .Include(escalation => escalation.Ticket).ThenInclude(ticket => ticket.CriticalityLevel)
            .Include(escalation => escalation.Initiator)
            .OrderByDescending(escalation => escalation.EscalatedAt)
            .ThenByDescending(escalation => escalation.Id)
            .ToListAsync(ct);

        return pending.Select(ToPendingEscalation).ToArray();
    }

    public async Task<TicketEscalationResponse?> AcceptEscalationAsync(
        int id,
        AcceptEscalationRequest request,
        int actorId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var ticket = await TicketQuery().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (ticket is null)
            return null;

        var openEscalation = await db.TicketEscalations
            .Where(escalation => escalation.TicketId == ticket.Id && escalation.AcceptedAt == null)
            .OrderByDescending(escalation => escalation.EscalatedAt)
            .ThenByDescending(escalation => escalation.Id)
            .FirstOrDefaultAsync(ct);

        if (ticket.Status.Code != EscalatedStatusCode || openEscalation is null)
            throw new InvalidOperationException("Заявка не находится в статусе «Эскалирована».");

        var headSetting = await db.ServiceDeskHeadSettings
            .FirstOrDefaultAsync(setting => setting.Id == 1, ct);
        if (!isAdmin && headSetting?.UserId != actorId)
            throw new UnauthorizedAccessException("Принять эскалацию может только руководитель службы поддержки или администратор.");

        var isActiveAgent = await db.UserRoleMappings
            .AnyAsync(mapping => mapping.UserId == actorId && mapping.Role.Code == "agent", ct);
        if (!isActiveAgent)
            throw new UnauthorizedAccessException("Принять эскалацию может только активный инженер технической поддержки.");

        var inProgressStatus = await db.TicketStatuses
            .FirstOrDefaultAsync(status => status.Code == InProgressStatusCode, ct)
            ?? throw new InvalidOperationException("Не настроен статус «В работе».");

        var now = DateTimeOffset.UtcNow;
        var oldStatusId = ticket.StatusId;

        openEscalation.AcceptedByUserId = actorId;
        openEscalation.AcceptedAt = now;
        openEscalation.Comment = NormalizeComment(request.Comment);

        ticket.StatusId = inProgressStatus.Id;
        ticket.Status = inProgressStatus;
        ticket.AssigneeId = actorId;
        ticket.Assignee = await db.Users.FirstAsync(user => user.Id == actorId, ct);
        ticket.AssigneeGroupId = null;
        ticket.UpdatedAt = now;

        AddAudit(ticket, actorId, "statusId", oldStatusId.ToString(), inProgressStatus.Id.ToString());
        AddAudit(ticket, actorId, "assigneeId", null, actorId.ToString());
        AddAudit(ticket, actorId, "escalationAccepted", null, openEscalation.Id.ToString());

        var initiatorNotified = openEscalation.InitiatorUserId is not null
            && openEscalation.InitiatorUserId != actorId;
        if (initiatorNotified)
        {
            await CreateNotificationAsync(
                openEscalation.InitiatorUserId!.Value,
                "TicketEscalationAccepted",
                $"Эскалация по заявке «{ticket.Title}» принята. Заявка передана в работу.",
                ticket.Id,
                ct);
        }

        await db.SaveChangesAsync(ct);

        return new TicketEscalationResponse(
            openEscalation.Id,
            openEscalation.TicketId,
            ticket.TicketNumber,
            openEscalation.InitiatorUserId,
            openEscalation.Reason,
            openEscalation.FromCriticalityLevelId,
            openEscalation.ToCriticalityLevelId,
            ParseNotifiedUserIds(openEscalation.NotifiedUserIds),
            openEscalation.EscalatedAt,
            openEscalation.AcceptedByUserId,
            openEscalation.AcceptedAt,
            openEscalation.Comment);
    }

    public async Task<TicketAttachmentUploadResponse?> UploadAttachmentAsync(
        int ticketId,
        int userId,
        bool isStaff,
        string fileName,
        Stream content,
        long sizeBytes,
        CancellationToken ct = default)
    {
        var ticket = await db.Tickets
            .FirstOrDefaultAsync(item => item.Id == ticketId, ct);
        if (ticket is null || (!isStaff && ticket.CreatorId != userId && ticket.RequesterId != userId))
            return null;
        if (sizeBytes is <= 0 or > MaxAttachmentSizeBytes)
            throw new ArgumentException("Размер вложения должен быть от 1 байта до 10 МБ.");

        var safeName = Path.GetFileName(fileName.Replace('\\', '/'));
        safeName = new string(safeName.Where(character => !char.IsControl(character)).ToArray()).Trim();
        if (safeName.Length == 0)
            throw new ArgumentException("Не удалось определить имя файла.");
        if (safeName.Length > 255)
            safeName = safeName[..255];

        var now = DateTimeOffset.UtcNow;
        var expiredUploads = await db.TicketAttachmentUploads
            .Where(upload => upload.ExpiresAt <= now
                && upload.Ticket.TenantId == db.GetCurrentTenantId())
            .ToListAsync(ct);
        foreach (var expiredUpload in expiredUploads)
            await attachmentStorage.DeleteAsync(expiredUpload.StorageKey, ct);
        db.TicketAttachmentUploads.RemoveRange(expiredUploads);

        var storageKey = await attachmentStorage.SaveAsync(content, ct);
        var upload = new TicketAttachmentUpload
        {
            TicketId = ticketId,
            UploadedById = userId,
            FileName = safeName,
            StorageKey = storageKey,
            SizeBytes = sizeBytes,
            UploadedAt = now,
            ExpiresAt = now.AddHours(24)
        };

        try
        {
            db.TicketAttachmentUploads.Add(upload);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await attachmentStorage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }

        return new TicketAttachmentUploadResponse(
            upload.Id.ToString("N"),
            upload.FileName,
            upload.SizeBytes,
            upload.ExpiresAt);
    }

    public async Task<TicketAttachmentFile?> OpenAttachmentAsync(
        int ticketId,
        int attachmentId,
        int userId,
        bool isStaff,
        CancellationToken ct = default)
    {
        var attachment = await db.Attachments
            .Where(item => item.Id == attachmentId
                && item.TicketMessage.TicketId == ticketId
                && item.TicketMessage.Ticket.TenantId == db.GetCurrentTenantId()
                && (isStaff || ((item.TicketMessage.Ticket.CreatorId == userId || item.TicketMessage.Ticket.RequesterId == userId) && item.TicketMessage.IsInternal != true)))
            .Select(item => new { item.FileName, item.FilePath })
            .FirstOrDefaultAsync(ct);
        if (attachment is null)
            return null;

        var content = await attachmentStorage.OpenReadAsync(attachment.FilePath, ct);
        return content is null ? null : new TicketAttachmentFile(content, attachment.FileName);
    }

    public async Task<IReadOnlyList<TicketMessageResponse>?> GetMessagesAsync(
        int id,
        int userId,
        bool isStaff,
        CancellationToken ct = default,
        int? skip = null,
        int? take = null)
    {
        var ticketOwner = await db.Tickets
            .Where(ticket => ticket.Id == id)
            .Select(ticket => new { CreatorId = (int?)ticket.CreatorId, RequesterId = (int?)ticket.RequesterId })
            .FirstOrDefaultAsync(ct);
        if (ticketOwner is null || (!isStaff && ticketOwner.CreatorId != userId && ticketOwner.RequesterId != userId))
            return null;

        var messagesQuery = db.TicketMessages
            .Where(message => message.TicketId == id && (isStaff || message.IsInternal != true))
            .Include(message => message.Author)
            .Include(message => message.Attachments);

        if (take.HasValue)
        {
            var page = await messagesQuery
                .OrderByDescending(message => message.CreatedAt)
                .ThenByDescending(message => message.Id)
                .Skip(skip ?? 0)
                .Take(take.Value)
                .ToListAsync(ct);
            page.Reverse();
            return page.Select(ToMessageResponse).ToArray();
        }

        var messages = await messagesQuery
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .ToListAsync(ct);

        return messages.Select(ToMessageResponse).ToArray();
    }

    public async Task<IReadOnlyList<TicketHistoryResponse>?> GetHistoryAsync(
        int id,
        int userId,
        bool isStaff,
        CancellationToken ct = default,
        int? skip = null,
        int? take = null)
    {
        var ticket = await db.Tickets
            .Where(item => item.Id == id && (isStaff || item.CreatorId == userId || item.RequesterId == userId))
            .Select(item => new { item.Id, item.TenantId })
            .FirstOrDefaultAsync(ct);
        if (ticket is null)
            return null;

        IQueryable<TicketAuditLog> auditQuery = db.TicketAuditLogs
            .Where(log => log.TicketId == id && log.Ticket.TenantId == ticket.TenantId)
            .Include(log => log.User);

        if (take.HasValue)
            auditQuery = auditQuery
                .OrderByDescending(log => log.ChangedAt)
                .ThenByDescending(log => log.Id)
                .Skip(skip ?? 0)
                .Take(take.Value);
        else
            auditQuery = auditQuery.OrderByDescending(log => log.ChangedAt).ThenByDescending(log => log.Id);

        var auditLogs = await auditQuery.ToListAsync(ct);

        return auditLogs.Select(log => new TicketHistoryResponse(
            log.Id,
            FormatAuditEvent(log),
            log.User.FullName,
            log.ChangedAt)).ToArray();
    }

    public async Task<TicketMessageResponse?> CreateMessageAsync(
        int id,
        CreateTicketMessageRequest request,
        int authorId,
        bool isStaff,
        CancellationToken ct = default)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (ticket is null || (!isStaff && ticket.CreatorId != authorId && ticket.RequesterId != authorId))
            return null;

        if (request.IsInternal && !isStaff)
            throw new UnauthorizedAccessException("Внутренние сообщения доступны только сотрудникам службы поддержки.");

        var uploadIds = new List<Guid>();
        foreach (var attachmentId in request.Attachments ?? [])
        {
            if (!Guid.TryParseExact(attachmentId, "N", out var parsedId))
                throw new ArgumentException("Идентификаторы вложений должны быть токенами, полученными при загрузке.");
            uploadIds.Add(parsedId);
        }
        if (uploadIds.Count != uploadIds.Distinct().Count())
            throw new ArgumentException("Нельзя прикрепить одно и то же вложение несколько раз.");

        var uploads = uploadIds.Count == 0
            ? []
            : await db.TicketAttachmentUploads
                .Where(upload => upload.TicketId == id
                    && upload.UploadedById == authorId
                    && upload.ExpiresAt > DateTimeOffset.UtcNow
                    && uploadIds.Contains(upload.Id))
                .ToListAsync(ct);
        if (uploads.Count != uploadIds.Count)
            throw new ArgumentException("Одно или несколько вложений не найдены, истекли или были загружены другим пользователем.");

        var message = new TicketMessage
        {
            TicketId = id,
            AuthorId = authorId,
            MessageBody = request.MessageBody.Trim(),
            IsInternal = request.IsInternal,
            SourceType = "web",
            CreatedAt = DateTimeOffset.UtcNow
        };
        if (isStaff && ticket.SlaPolicyId is not null)
        {
            var now = message.CreatedAt;
            RecordSlaBreaches(ticket, now);
            ticket.ActualReactionTime ??= now;
        }
        db.TicketMessages.Add(message);
        foreach (var upload in uploads)
        {
            db.Attachments.Add(new Attachment
            {
                TicketMessage = message,
                FileName = upload.FileName,
                FilePath = upload.StorageKey,
                ContentType = "application/octet-stream",
                SizeBytes = upload.SizeBytes,
                UploadedById = authorId,
                UploadedAt = DateTimeOffset.UtcNow
            });
        }
        db.TicketAttachmentUploads.RemoveRange(uploads);
        await db.SaveChangesAsync(ct);

        await db.Entry(message).Reference(item => item.Author).LoadAsync(ct);
        await db.Entry(message).Collection(item => item.Attachments).LoadAsync(ct);
        if (!request.IsInternal)
        {
            await db.Entry(ticket).Reference(item => item.Assignee).LoadAsync(ct);
            await NotifyRecipientsAsync(
                ticket,
                authorId,
                "TicketCommentAdded",
                $"В заявке «{ticket.Title}» добавлено новое сообщение.",
                ct);
        }
        return ToMessageResponse(message);
    }

    public async Task<IReadOnlyList<AssetTicketHistoryItem>?> GetAssetTicketHistoryAsync(
        int assetId,
        string? search,
        int? typeId,
        int? statusId,
        int? criticalityLevelId,
        int? assigneeId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        CancellationToken ct = default)
    {
        if (!await db.Assets.AnyAsync(asset => asset.Id == assetId, ct))
            return null;
        if (dateFrom > dateTo)
            throw new ArgumentException("dateFrom не может быть позже dateTo.");

        var query = TicketQuery()
            .Where(ticket => ticket.AssetId == assetId
                || db.TicketAssetMappings.Any(mapping => mapping.TicketId == ticket.Id && mapping.AssetId == assetId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(ticket =>
                EF.Functions.ILike(ticket.TicketNumber, term)
                || EF.Functions.ILike(ticket.Title, term));
        }

        if (typeId.HasValue)
            query = query.Where(ticket => ticket.TypeId == typeId.Value);
        if (statusId.HasValue)
            query = query.Where(ticket => ticket.StatusId == statusId.Value);
        if (criticalityLevelId.HasValue)
            query = query.Where(ticket => ticket.CriticalityLevelId == criticalityLevelId.Value);
        if (assigneeId.HasValue)
            query = query.Where(ticket => ticket.AssigneeId == assigneeId.Value);
        if (dateFrom.HasValue)
        {
            var start = new DateTimeOffset(dateFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            query = query.Where(ticket => ticket.CreatedAt >= start);
        }
        if (dateTo.HasValue)
        {
            var endExclusive = new DateTimeOffset(dateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            query = query.Where(ticket => ticket.CreatedAt < endExclusive);
        }

        var tickets = await query.OrderByDescending(ticket => ticket.CreatedAt).ToListAsync(ct);
        return tickets.Select(ticket => new AssetTicketHistoryItem(
            ticket.Id,
            ticket.TicketNumber,
            ticket.TypeId,
            ticket.Type.Name,
            ticket.Title,
            ticket.CreatorId,
            ticket.Creator.FullName,
            ticket.RequesterId,
            ticket.Requester?.FullName,
            ticket.AssigneeId,
            ticket.Assignee?.FullName,
            ticket.StatusId,
            ticket.Status.Name,
            ticket.CriticalityLevelId,
            ticket.CriticalityLevel?.Name,
            ticket.CreatedAt,
            ticket.ActualResolutionTime)).ToArray();
    }

    public async Task<ServiceDeskHeadSettingResponse?> GetServiceDeskHeadAsync(CancellationToken ct = default)
    {
        var setting = await db.ServiceDeskHeadSettings
            .Where(item => item.Id == 1)
            .Select(item => new ServiceDeskHeadSettingResponse(
                item.UserId,
                item.User.Username,
                item.User.FullName))
            .FirstOrDefaultAsync(ct);

        return setting;
    }

    public async Task<ServiceDeskHeadSettingResponse?> SetServiceDeskHeadAsync(
        SetServiceDeskHeadRequest request,
        CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, ct);
        if (user is null)
            return null;

        var isActiveAgent = user.IsActive && await db.UserRoleMappings
            .AnyAsync(mapping => mapping.UserId == user.Id && mapping.Role.Code == "agent", ct);
        if (!isActiveAgent)
            throw new ArgumentException("Руководителем службы поддержки может быть только активный инженер.");

        var setting = await db.ServiceDeskHeadSettings.FirstOrDefaultAsync(item => item.Id == 1, ct);
        if (setting is null)
        {
            setting = new ServiceDeskHeadSetting
            {
                Id = 1,
                TenantId = db.GetCurrentTenantId(),
                UserId = user.Id
            };
            db.ServiceDeskHeadSettings.Add(setting);
        }
        else
        {
            setting.UserId = user.Id;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return new ServiceDeskHeadSettingResponse(user.Id, user.Username, user.FullName);
    }

    public async Task<IReadOnlyList<EscalationRuleResponse>> GetEscalationRulesAsync(CancellationToken ct = default)
    {
        var ticketTypes = await db.TicketTypes
            .AsNoTracking()
            .OrderBy(type => type.Id)
            .ToListAsync(ct);
        var rules = await db.EscalationRules
            .AsNoTracking()
            .ToDictionaryAsync(rule => rule.TicketTypeId, ct);

        return ticketTypes.Select(type =>
        {
            if (rules.TryGetValue(type.Id, out var rule))
                return new EscalationRuleResponse(type.Id, rule.OnSlaBreach, rule.OnManual, rule.IsActive);

            return new EscalationRuleResponse(type.Id, OnSlaBreach: true, OnManual: true, IsActive: true);
        }).ToArray();
    }

    public async Task<EscalationRuleResponse?> SetEscalationRuleAsync(
        int ticketTypeId,
        UpdateEscalationRuleRequest request,
        CancellationToken ct = default)
    {
        if (!await db.TicketTypes.AnyAsync(type => type.Id == ticketTypeId, ct))
            return null;

        var rule = await db.EscalationRules
            .FirstOrDefaultAsync(item => item.TicketTypeId == ticketTypeId, ct);
        if (rule is null)
        {
            rule = new EscalationRule { TicketTypeId = ticketTypeId };
            db.EscalationRules.Add(rule);
        }

        rule.OnSlaBreach = request.OnSlaBreach;
        rule.OnManual = request.OnManual;
        rule.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);

        return new EscalationRuleResponse(
            rule.TicketTypeId,
            rule.OnSlaBreach,
            rule.OnManual,
            rule.IsActive);
    }

    public async Task<int?> ResolveTicketIdAsync(string ticketNumberOrId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ticketNumberOrId))
            return null;

        var value = ticketNumberOrId.Trim();
        var normalized = value.ToUpperInvariant();

        var byNumber = await db.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.TicketNumber == normalized)
            .Select(ticket => (int?)ticket.Id)
            .FirstOrDefaultAsync(ct);
        if (byNumber is not null)
            return byNumber;

        return int.TryParse(value, out var numericId)
            ? await db.Tickets.AsNoTracking().Where(ticket => ticket.Id == numericId).Select(ticket => (int?)ticket.Id).FirstOrDefaultAsync(ct)
            : null;
    }

    private async Task ConfigureSlaAsync(
        Ticket ticket,
        int criticalityLevelId,
        DateTimeOffset createdAt,
        CancellationToken ct)
    {
        var slaValue = await db.SlaValues.AsNoTracking().FirstOrDefaultAsync(
            value => value.TicketTypeId == ticket.TypeId
                && value.CriticalityLevelId == criticalityLevelId,
            ct);
        if (slaValue is null || slaValue.IsDisabled
            || (slaValue.SlaPolicyId is null
                && slaValue.ReactionTimeMinutes is null
                && slaValue.ResolutionTimeMinutes is null))
            return;

        if (slaValue.ReactionTimeMinutes is <= 0 || slaValue.ResolutionTimeMinutes is <= 0)
            throw new InvalidOperationException("В SLA должны быть заданы только положительные длительности.");

        var policies = db.SlaPolicies.AsNoTracking();
        if (slaValue.SlaPolicyId is int configuredPolicyId)
        {
            policies = policies.Where(policy => policy.Id == configuredPolicyId);
        }
        else
        {
            if (slaValue.ReactionTimeMinutes is int reactionMinutes)
                policies = policies.Where(policy => policy.ReactionTimeMinutes == reactionMinutes);
            if (slaValue.ResolutionTimeMinutes is int resolutionMinutes)
                policies = policies.Where(policy => policy.ResolutionTimeMinutes == resolutionMinutes);
        }

        var policy = await policies.OrderBy(item => item.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                $"Для типа заявки {ticket.TypeId} и критичности {criticalityLevelId} не найдена SLA-политика с заданными сроками.");

        var reactionDuration = slaValue.ReactionTimeMinutes ?? policy.ReactionTimeMinutes;
        var resolutionDuration = slaValue.ResolutionTimeMinutes ?? policy.ResolutionTimeMinutes;
        if (reactionDuration <= 0 || resolutionDuration <= 0)
            throw new InvalidOperationException("Длительности реакции и решения в SLA должны быть больше нуля.");

        var scheduleType = string.IsNullOrWhiteSpace(policy.ScheduleType)
            ? "24x7"
            : policy.ScheduleType;
        var tenantId = db.GetCurrentTenantId();
        var schedule = await db.WorkingSchedules.AsNoTracking()
            .Where(item => item.IsActive
                && item.ScheduleType == scheduleType
                && (item.TenantId == tenantId || item.TenantId == null))
            .OrderByDescending(item => item.TenantId == tenantId)
            .ThenBy(item => item.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Не найдено активное расписание SLA типа «{scheduleType}».");
        var hours = await db.WorkingHours.AsNoTracking()
            .Where(item => item.WorkingScheduleId == schedule.Id)
            .ToArrayAsync(ct);
        var exceptions = (await db.CalendarExceptions.AsNoTracking()
            .Where(item => item.TenantId == tenantId || item.TenantId == null)
            .OrderBy(item => item.ExceptionDate)
            .ToArrayAsync(ct))
            .GroupBy(item => item.ExceptionDate)
            .Select(group => group.OrderByDescending(item => item.TenantId == tenantId).First())
            .ToArray();
        var calendar = SlaCalendarCalculator.CreateSnapshot(schedule, hours, exceptions);

        ticket.SlaPolicyId = policy.Id;
        ticket.SlaCalendarSnapshot = SlaCalendarCalculator.Serialize(calendar);
        ticket.SlaReactionStartedAt = createdAt;
        ticket.SlaResolutionStartedAt = createdAt;
        ticket.SlaReactionDeadline = SlaCalendarCalculator.AddWorkingMinutes(
            createdAt,
            reactionDuration,
            calendar);
        ticket.SlaResolutionDeadline = SlaCalendarCalculator.AddWorkingMinutes(
            createdAt,
            resolutionDuration,
            calendar);
    }

    private static void ResumeSlaTimers(Ticket ticket, DateTimeOffset resumedAt)
    {
        var pauseStartedAt = ticket.SlaPausedAt;
        if (pauseStartedAt is not null && ticket.SlaCalendarSnapshot is not null)
        {
            var calendar = SlaCalendarCalculator.Deserialize(ticket.SlaCalendarSnapshot);
            var pausedWorkingTime = SlaCalendarCalculator.WorkingTimeBetween(
                pauseStartedAt.Value,
                resumedAt,
                calendar);
            ticket.SlaAccumulatedPauseMinutes = checked(
                ticket.SlaAccumulatedPauseMinutes + (int)Math.Floor(pausedWorkingTime.TotalMinutes));

            if (ticket.ActualReactionTime is null && ticket.SlaReactionDeadline is { } reactionDeadline)
            {
                if (ticket.SlaReactionBreachedAt is null && pauseStartedAt < reactionDeadline)
                    ticket.SlaReactionDeadline = SlaCalendarCalculator.AddWorkingTime(
                        reactionDeadline,
                        pausedWorkingTime,
                        calendar);
                else if (reactionDeadline <= pauseStartedAt)
                    ticket.SlaReactionBreachedAt ??= reactionDeadline;
            }

            if (ticket.ActualResolutionTime is null && ticket.SlaResolutionDeadline is { } resolutionDeadline)
            {
                if (ticket.SlaResolutionBreachedAt is null && pauseStartedAt < resolutionDeadline)
                    ticket.SlaResolutionDeadline = SlaCalendarCalculator.AddWorkingTime(
                        resolutionDeadline,
                        pausedWorkingTime,
                        calendar);
                else if (resolutionDeadline <= pauseStartedAt)
                    ticket.SlaResolutionBreachedAt ??= resolutionDeadline;
            }
        }

        ticket.SlaPausedAt = null;
    }

    private static void RecordSlaBreaches(Ticket ticket, DateTimeOffset at)
    {
        if (ticket.IsSlaPaused)
            return;

        if (ticket.ActualReactionTime is null
            && ticket.SlaReactionDeadline is { } reactionDeadline
            && reactionDeadline <= at)
            ticket.SlaReactionBreachedAt ??= reactionDeadline;

        if (ticket.ActualResolutionTime is null
            && ticket.SlaResolutionDeadline is { } resolutionDeadline
            && resolutionDeadline <= at)
            ticket.SlaResolutionBreachedAt ??= resolutionDeadline;
    }

    private async Task<string> GenerateTicketNumberAsync(int tenantId, CancellationToken ct)
    {
        // Коллизии маловероятны, но проверяем явно, чтобы отдать понятную ошибку,
        // а не нарушение уникального индекса.
        var taken = await db.Tickets
            .IgnoreQueryFilters()
            .Where(ticket => ticket.TenantId == tenantId)
            .Select(ticket => ticket.TicketNumber)
            .ToHashSetAsync(ct);

        return TicketNumberGenerator.GenerateUnique(taken.Contains);
    }

    private IQueryable<Ticket> TicketQuery() =>
        db.Tickets
            .Include(ticket => ticket.Type)
            .Include(ticket => ticket.Status)
            .Include(ticket => ticket.Creator)
            .Include(ticket => ticket.Requester)
            .Include(ticket => ticket.Assignee)
            .Include(ticket => ticket.CriticalityLevel);

    private async Task ValidateActiveAgentAsync(int userId, CancellationToken ct)
    {
        var isActiveAgent = await db.Users
            .AnyAsync(user => user.Id == userId && user.IsActive
                && user.RoleMappings.Any(mapping => mapping.Role.Code == "agent"), ct);
        if (!isActiveAgent)
            throw new ArgumentException("Исполнитель должен быть активным инженером технической поддержки.");
    }

    private static JsonElement? CloneObject(JsonElement? element)
    {
        if (element is not JsonElement value)
            return null;
        if (value.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("customFieldsData должен быть JSON-объектом.");

        return value.Clone();
    }

    private async Task NotifyRecipientsAsync(
        Ticket ticket,
        int actorId,
        string type,
        string message,
        CancellationToken ct)
    {
        var recipientIds = new HashSet<int> { ticket.CreatorId };
        if (ticket.RequesterId is int requesterId)
            recipientIds.Add(requesterId);
        if (ticket.AssigneeId is int assigneeId)
            recipientIds.Add(assigneeId);
        recipientIds.Remove(actorId);

        foreach (var recipientId in recipientIds)
            await CreateNotificationAsync(recipientId, type, message, ticket.Id, ct);
    }

    private async Task CreateNotificationAsync(
        int userId,
        string type,
        string message,
        int? ticketId,
        CancellationToken ct)
    {
        if (notificationService is not null)
        {
            await notificationService.CreateNotification(userId, type, message, ticketId, ct);
            return;
        }

        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            Title = message,
            Body = null,
            TicketId = ticketId,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static void AddAudit(Ticket ticket, int userId, string field, string? oldValue, string? newValue)
    {
        if (oldValue == newValue)
            return;

        ticket.AuditLogs.Add(new TicketAuditLog
        {
            UserId = userId,
            FieldName = field,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedAt = DateTimeOffset.UtcNow
        });
    }

    private static string FormatAuditEvent(TicketAuditLog log)
    {
        if (log.FieldName == "created")
            return "Заявка создана";

        var fieldName = log.FieldName switch
        {
            "priority" => "Приоритет",
            "statusId" => "Статус",
            "assigneeId" => "Исполнитель",
            "requesterId" => "Заявитель",
            "criticalityLevelId" => "Критичность",
            "autoAssigned" => "Автораспределение",
            "escalationAccepted" => "Эскалация принята",
            _ => log.FieldName
        };
        var change = log.OldValue is null
            ? log.NewValue
            : $"{log.OldValue} → {log.NewValue}";
        return $"{fieldName} изменен: {change}";
    }

    private static TicketResponse ToResponse(Ticket ticket) => new(
        ticket.Id,
        ticket.TicketNumber,
        ticket.Title,
        ticket.Description,
        ticket.TypeId,
        ticket.Type.Name,
        ticket.StatusId,
        ticket.Status.Name,
        ticket.Priority,
        ticket.CriticalityLevelId,
        ticket.ServiceId,
        ticket.CreatorId,
        ticket.Creator.FullName,
        ticket.RequesterId,
        ticket.Requester?.FullName,
        ticket.AssigneeId,
        ticket.Assignee?.FullName,
        ticket.AssigneeGroupId,
        ticket.AssetId,
        ticket.CustomFieldsData is JsonElement fields ? fields.Clone() : null,
        ticket.CreatedAt,
        ticket.UpdatedAt,
        ticket.SlaReactionDeadline,
        ticket.SlaResolutionDeadline);

    private static TicketMessageResponse ToMessageResponse(TicketMessage message) => new(
        message.Id,
        message.TicketId,
        message.AuthorId,
        message.Author.FullName,
        message.MessageBody,
        message.IsInternal ?? false,
        message.SourceType,
        message.Attachments.Select(attachment => new TicketAttachmentResponse(attachment.Id, attachment.FileName)).ToArray(),
        message.CreatedAt);

    private static PendingEscalationResponse ToPendingEscalation(TicketEscalation escalation)
    {
        var ticket = escalation.Ticket;

        return new PendingEscalationResponse(
            escalation.Id,
            ticket.Id,
            ticket.TicketNumber,
            ticket.Title,
            ticket.TypeId,
            ticket.Type.Name,
            ticket.StatusId,
            ticket.Status.Name,
            ticket.CriticalityLevelId,
            ticket.CriticalityLevel?.Name,
            ticket.EscalationLevel,
            ticket.AssigneeId,
            ticket.Assignee?.FullName,
            escalation.InitiatorUserId,
            escalation.Initiator?.FullName,
            escalation.EscalatedAt,
            escalation.AcceptedByUserId,
            escalation.AcceptedAt);
    }

    private static string? NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return null;

        return comment.Trim();
    }

    private static IReadOnlyList<int> ParseNotifiedUserIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        try
        {
            return JsonSerializer.Deserialize<int[]>(raw) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

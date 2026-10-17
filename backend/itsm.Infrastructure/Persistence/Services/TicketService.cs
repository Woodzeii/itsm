using System.Text.Json;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Persistence.Services;

public sealed class TicketService(ItsmDbContext db, ITicketAttachmentStorage attachmentStorage) : ITicketService
{
    private const string RepairTypeCode = "repair";
    private const string ClosedStatusCode = "closed";
    private const long MaxAttachmentSizeBytes = 10 * 1024 * 1024;

    public async Task<IReadOnlyList<TicketResponse>> GetQueueAsync(CancellationToken ct = default)
    {
        var tickets = await TicketQuery()
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ToListAsync(ct);

        return tickets.Select(ToResponse).ToArray();
    }

    public async Task<TicketResponse?> GetByIdAsync(
        int id,
        int userId,
        bool isStaff,
        CancellationToken ct = default)
    {
        var ticket = await TicketQuery()
            .FirstOrDefaultAsync(ticket => ticket.Id == id, ct);

        if (ticket is null || (!isStaff && ticket.CreatorId != userId))
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
        var type = await db.TicketTypes.FirstOrDefaultAsync(type => type.Id == request.TypeId, ct)
            ?? throw new ArgumentException("Тип заявки не найден.");

        if (!isStaff && (!type.IsPortalAvailable || type.Code == RepairTypeCode))
            throw new UnauthorizedAccessException("Создание заявок этого типа недоступно пользователю портала.");

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

        var initialStatusId = await db.TicketStatuses
            .Where(status => status.Code == "new")
            .Select(status => (int?)status.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Не настроен начальный статус заявок.");

        var now = DateTimeOffset.UtcNow;
        var ticket = new Ticket
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            TypeId = type.Id,
            StatusId = initialStatusId,
            Priority = request.Priority.Trim(),
            CreatorId = creatorId,
            AssetId = request.AssetId,
            CustomFieldsData = CloneObject(request.CustomFieldsData),
            CreatedAt = now,
            UpdatedAt = now
        };

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

            var newStage = request.RepairOutcome.Value switch
            {
                RepairOutcome.ReturnToExploitation => AssetLifecycleStages.InUse,
                RepairOutcome.TransferToWarehouse => AssetLifecycleStages.InStock,
                RepairOutcome.Retire => AssetLifecycleStages.Decommissioned,
                _ => throw new ArgumentOutOfRangeException(nameof(request.RepairOutcome))
            };

            var oldStage = asset.LifecycleStage;
            asset.LifecycleStage = newStage;
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
                    repairResolution = request.RepairResolution.Trim()
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
        else if (!isRepair && (request.RepairResolution is not null || request.RepairOutcome is not null))
        {
            throw new ArgumentException("Результат ремонта можно передавать только для заявки на ремонт.");
        }

        var oldStatusId = ticket.StatusId;
        var now = DateTimeOffset.UtcNow;
        ticket.StatusId = targetStatus.Id;
        ticket.Status = targetStatus;
        ticket.IsSlaPaused = targetStatus.IsSlaPausing;
        ticket.SlaTimerPaused = targetStatus.IsSlaPausing;
        if (targetStatus.Code == ClosedStatusCode)
            ticket.ActualResolutionTime = now;
        ticket.UpdatedAt = now;
        AddAudit(ticket, actorId, "statusId", oldStatusId.ToString(), targetStatus.Id.ToString());

        await db.SaveChangesAsync(ct);
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

        await ValidateActiveAgentAsync(request.AssigneeId, ct);
        var oldAssigneeId = ticket.AssigneeId;
        ticket.AssigneeId = request.AssigneeId;
        ticket.Assignee = await db.Users.FirstAsync(user => user.Id == request.AssigneeId, ct);
        ticket.AssigneeGroupId = null;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit(ticket, actorId, "assigneeId", oldAssigneeId?.ToString(), request.AssigneeId.ToString());

        await db.SaveChangesAsync(ct);
        var updatedTicket = await TicketQuery().FirstAsync(item => item.Id == ticket.Id, ct);
        return ToResponse(updatedTicket);
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
            .Where(mapping => mapping.Role.Code == "admin" && mapping.User.IsActive)
            .Select(mapping => mapping.UserId)
            .Distinct()
            .OrderBy(userId => userId)
            .ToListAsync(ct);
        if (!notifiedUserIds.Contains(headSetting.UserId))
            notifiedUserIds.Add(headSetting.UserId);

        var now = DateTimeOffset.UtcNow;
        var fromCriticalityId = ticket.CriticalityLevelId;
        var toCriticalityId = nextCriticality?.Id ?? fromCriticalityId;
        if (toCriticalityId != fromCriticalityId)
        {
            ticket.CriticalityLevelId = toCriticalityId;
            AddAudit(ticket, actorId, "criticalityLevelId", fromCriticalityId?.ToString(), toCriticalityId?.ToString());
        }

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
            db.Notifications.Add(new Notification
            {
                UserId = recipientId,
                Type = "TicketEscalated",
                Title = "Эскалация заявки",
                Body = $"Заявка «{ticket.Title}» была эскалирована.",
                TicketId = ticket.Id,
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
        return new TicketEscalationResponse(
            escalation.Id,
            escalation.TicketId,
            escalation.InitiatorUserId,
            escalation.Reason,
            escalation.FromCriticalityLevelId,
            escalation.ToCriticalityLevelId,
            notifiedUserIds,
            escalation.EscalatedAt);
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
        if (ticket is null || (!isStaff && ticket.CreatorId != userId))
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
            .Where(upload => upload.ExpiresAt <= now)
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
                && (isStaff || (item.TicketMessage.Ticket.CreatorId == userId && item.TicketMessage.IsInternal != true)))
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
        CancellationToken ct = default)
    {
        var ticketOwnerId = await db.Tickets
            .Where(ticket => ticket.Id == id)
            .Select(ticket => (int?)ticket.CreatorId)
            .FirstOrDefaultAsync(ct);
        if (ticketOwnerId is null || (!isStaff && ticketOwnerId != userId))
            return null;

        var messages = await db.TicketMessages
            .Where(message => message.TicketId == id && (isStaff || message.IsInternal != true))
            .Include(message => message.Author)
            .Include(message => message.Attachments)
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .ToListAsync(ct);

        return messages.Select(ToMessageResponse).ToArray();
    }

    public async Task<TicketMessageResponse?> CreateMessageAsync(
        int id,
        CreateTicketMessageRequest request,
        int authorId,
        bool isStaff,
        CancellationToken ct = default)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (ticket is null || (!isStaff && ticket.CreatorId != authorId))
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
        return ToMessageResponse(message);
    }

    public async Task<IReadOnlyList<AssetTicketHistoryItem>?> GetAssetTicketHistoryAsync(
        int assetId,
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
            ticket.TypeId,
            ticket.Type.Name,
            ticket.Title,
            ticket.CreatorId,
            ticket.Creator.FullName,
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
            setting = new ServiceDeskHeadSetting { Id = 1, UserId = user.Id };
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

    private IQueryable<Ticket> TicketQuery() =>
        db.Tickets
            .Include(ticket => ticket.Type)
            .Include(ticket => ticket.Status)
            .Include(ticket => ticket.Creator)
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

    private static TicketResponse ToResponse(Ticket ticket) => new(
        ticket.Id,
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
}

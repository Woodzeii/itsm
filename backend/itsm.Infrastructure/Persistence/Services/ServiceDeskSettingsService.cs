using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Persistence.Services;

public sealed class ServiceDeskSettingsService(ItsmDbContext db) : IServiceDeskSettingsService
{
    private const int MinCriticalityLevels = 3;
    private const int MaxCriticalityLevels = 5;
    private const string ClosedStatusCode = "closed";
    private const string PortalUserRoleCode = "portal_user";

    public async Task<ServiceDeskSettingsDto> GetAsync(bool canManageAdminCreation, CancellationToken ct = default)
    {
        var tenant = await GetTenantAsync(ct);
        var policy = await db.PasswordPolicies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == 1, ct);
        var criticalities = await LoadCriticalitiesAsync(ct);
        var ticketTypes = await GetTicketTypesAsync(ct);
        var assignment = await db.AssignmentSettings.AsNoTracking().FirstOrDefaultAsync(item => item.Id == 1, ct);
        var manualAssignerName = assignment?.ManualAssignerUserId is int manualAssignerId
            ? await db.Users.Where(user => user.Id == manualAssignerId).Select(user => user.FullName).FirstOrDefaultAsync(ct)
            : null;
        var portalAccounts = await db.Users
            .AsNoTracking()
            .Where(user => user.RoleMappings.Any(mapping => mapping.Role.Code == PortalUserRoleCode))
            .OrderBy(user => user.Username)
            .Select(user => user.Username)
            .ToArrayAsync(ct);

        return new ServiceDeskSettingsDto
        {
            AllowAdminsCreateAdmins = tenant.AllowAdminsCreateAdmins,
            CanManageAdminCreation = canManageAdminCreation,
            SlaEnabled = tenant.SlaEnabled,
            AssignmentMode = assignment?.Mode ?? "auto",
            ManualAssignerUserId = assignment?.ManualAssignerUserId,
            ManualAssignerName = manualAssignerName,
            PortalSlaFields = new PortalSlaVisibilityDto
            {
                ShowReactionTime = tenant.PortalSlaReactionVisible,
                ShowResolutionTime = tenant.PortalSlaResolutionVisible
            },
            PasswordPolicy = ToPasswordPolicyDto(policy),
            MaxCriticalityLevels = MaxCriticalityLevels,
            MinCriticalityLevels = MinCriticalityLevels,
            Criticalities = criticalities,
            TicketTypes = ticketTypes,
            PortalAccounts = portalAccounts
        };
    }

    public async Task<ServiceDeskSettingsDto> UpdateAsync(
        UpdateServiceDeskSettingsRequest request,
        bool canManageAdminCreation,
        CancellationToken ct = default)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        var tenant = await GetTenantAsync(ct);

        if (request.AllowAdminsCreateAdmins is bool allowAdmins)
        {
            if (!canManageAdminCreation && allowAdmins != tenant.AllowAdminsCreateAdmins)
                throw new UnauthorizedAccessException("Настройку прав администраторов может изменять только владелец тенанта.");
            tenant.AllowAdminsCreateAdmins = allowAdmins;
        }

        if (request.SlaEnabled is bool slaEnabled)
            tenant.SlaEnabled = slaEnabled;

        if (request.AssignmentMode is string assignmentMode)
        {
            var mode = assignmentMode.Trim().ToLowerInvariant();
            if (mode is not ("auto" or "manual"))
                throw new ArgumentException("Режим назначения должен быть auto или manual.");

            var assignerId = mode == "manual" ? request.ManualAssignerUserId : null;
            if (assignerId is int requestedAssignerId)
            {
                var validAssigner = await db.Users.AnyAsync(user =>
                    user.Id == requestedAssignerId
                    && user.IsActive
                    && user.RoleMappings.Any(mapping =>
                        mapping.Role.Code == "admin"
                        || mapping.Role.Code == "tenant_owner"
                        || mapping.Role.Code == "manager"), ct);
                if (!validAssigner)
                    throw new ArgumentException("Ответственным за ручное назначение может быть только активный администратор или менеджер.");
            }

            var setting = await db.AssignmentSettings.FirstOrDefaultAsync(item => item.Id == 1, ct);
            if (setting is null)
            {
                setting = new AssignmentSetting { Id = 1, TenantId = db.CurrentTenantId };
                db.AssignmentSettings.Add(setting);
            }

            setting.Mode = mode;
            setting.ManualAssignerUserId = assignerId;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (request.PortalSlaFields is { } portal)
        {
            tenant.PortalSlaReactionVisible = portal.ShowReactionTime;
            tenant.PortalSlaResolutionVisible = portal.ShowResolutionTime;
        }

        if (request.PasswordPolicy is { } passwordPolicy)
        {
            ValidatePasswordPolicy(passwordPolicy);
            var existing = await db.PasswordPolicies.FirstOrDefaultAsync(item => item.Id == 1, ct);
            if (existing is null)
            {
                existing = new PasswordPolicy { Id = 1 };
                db.PasswordPolicies.Add(existing);
            }

            CopyPasswordPolicy(passwordPolicy, existing);
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (request.Criticalities is { } criticalities)
        {
            ValidateCriticalities(criticalities);
            await ApplyCriticalitiesAsync(criticalities, ct);
            await db.SaveChangesAsync(ct);
        }

        if (request.TicketTypes is { } ticketTypes)
            await ApplyTicketTypesAsync(ticketTypes, ct);

        await db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        return await GetAsync(canManageAdminCreation, ct);
    }

    public async Task<PasswordPolicyDto> GetPasswordPolicyAsync(CancellationToken ct = default)
    {
        var policy = await db.PasswordPolicies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == 1, ct);
        return ToPasswordPolicyDto(policy);
    }

    public async Task<PasswordPolicyDto> UpdatePasswordPolicyAsync(UpdatePasswordPolicyRequest request, CancellationToken ct = default)
    {
        ValidatePasswordPolicy(request);

        var policy = await db.PasswordPolicies.FirstOrDefaultAsync(item => item.Id == 1, ct);
        if (policy is null)
        {
            policy = new PasswordPolicy { Id = 1 };
            db.PasswordPolicies.Add(policy);
        }

        CopyPasswordPolicy(request, policy);
        policy.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToPasswordPolicyDto(policy);
    }

    public async Task<IReadOnlyList<CriticalityLevelDto>> GetCriticalityLevelsAsync(CancellationToken ct = default) =>
        await LoadCriticalitiesAsync(ct);

    public async Task<CriticalityLevelDto> CreateCriticalityLevelAsync(CreateCriticalityLevelRequest request, CancellationToken ct = default)
    {
        var activeCount = await db.CriticalityLevels.CountAsync(level => level.IsActive, ct);
        if (activeCount >= MaxCriticalityLevels)
            throw new InvalidOperationException($"Максимальное количество уровней критичности — {MaxCriticalityLevels}.");

        var code = NormalizeCode(request.Code, "level");
        await EnsureCriticalityCodeUniqueAsync(code, null, ct);
        await EnsureCriticalityNameUniqueAsync(request.Name, null, ct);

        var level = new CriticalityLevel
        {
            TenantId = db.CurrentTenantId,
            Code = code,
            Name = request.Name.Trim(),
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.CriticalityLevels.Add(level);
        await db.SaveChangesAsync(ct);
        return (await LoadCriticalitiesAsync(ct)).Single(item => item.Id == level.Id);
    }

    public async Task<CriticalityLevelDto> UpdateCriticalityLevelAsync(
        int id,
        UpdateCriticalityLevelRequest request,
        CancellationToken ct = default)
    {
        var level = await db.CriticalityLevels.FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new KeyNotFoundException("Уровень критичности не найден.");

        var activeCount = await db.CriticalityLevels.CountAsync(item => item.IsActive, ct);
        if (!request.IsActive && level.IsActive && activeCount - 1 < MinCriticalityLevels)
            throw new ArgumentException($"Должно оставаться не менее {MinCriticalityLevels} активных уровней критичности.");
        if (request.IsActive && !level.IsActive && activeCount + 1 > MaxCriticalityLevels)
            throw new InvalidOperationException($"Максимальное количество уровней критичности — {MaxCriticalityLevels}.");

        await EnsureCriticalityNameUniqueAsync(request.Name, id, ct);
        level.Name = request.Name.Trim();
        level.SortOrder = request.SortOrder;
        level.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return (await LoadCriticalitiesAsync(ct)).Single(item => item.Id == id);
    }

    public async Task DeleteCriticalityLevelAsync(int id, CancellationToken ct = default)
    {
        var level = await db.CriticalityLevels.FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new KeyNotFoundException("Уровень критичности не найден.");

        var activeCount = await db.CriticalityLevels.CountAsync(item => item.IsActive, ct);
        if (level.IsActive && activeCount - 1 < MinCriticalityLevels)
            throw new ArgumentException($"Должно оставаться не менее {MinCriticalityLevels} активных уровней критичности.");

        var assignedTickets = await db.Tickets.CountAsync(ticket => ticket.CriticalityLevelId == id, ct);
        if (assignedTickets > 0)
            throw new InvalidOperationException(
                $"Уровень «{level.Name}» назначен хотя бы одной заявке и не может быть удалён.");

        var escalations = await db.TicketEscalations
            .CountAsync(escalation => escalation.FromCriticalityLevelId == id || escalation.ToCriticalityLevelId == id, ct);
        if (escalations > 0)
            throw new InvalidOperationException(
                $"Уровень «{level.Name}» используется в истории эскалаций и не может быть удалён.");

        var slaValues = await db.SlaValues.Where(value => value.CriticalityLevelId == id).ToArrayAsync(ct);
        db.SlaValues.RemoveRange(slaValues);
        db.CriticalityLevels.Remove(level);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TicketTypeDto>> GetTicketTypesAsync(CancellationToken ct = default)
    {
        var types = await db.TicketTypes
            .AsNoTracking()
            .Where(type => !type.IsArchived)
            .OrderBy(type => type.Id)
            .ToArrayAsync(ct);
        var typeIds = types.Select(type => type.Id).ToArray();
        var slaValues = typeIds.Length == 0
            ? []
            : await db.SlaValues
                .AsNoTracking()
                .Where(value => typeIds.Contains(value.TicketTypeId))
                .OrderBy(value => value.CriticalityLevelId)
                .ToArrayAsync(ct);

        var closedStatusId = await db.TicketStatuses
            .Where(status => status.Code == ClosedStatusCode)
            .Select(status => (int?)status.Id)
            .FirstOrDefaultAsync(ct);
        var activeTicketCounts = await db.Tickets
            .Where(ticket => closedStatusId == null || ticket.StatusId != closedStatusId)
            .GroupBy(ticket => ticket.TypeId)
            .Select(group => new { TypeId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.TypeId, item => item.Count, ct);

        return types.Select(type => new TicketTypeDto
        {
            Id = type.Id,
            Code = type.Code,
            Name = type.Name,
            IsBuiltIn = type.IsBuiltIn,
            IsPortalAvailable = type.IsPortalAvailable,
            IsSlaEnabled = type.IsSlaEnabled,
            AssignmentGroupId = type.AssignmentGroupId,
            ActiveTicketCount = activeTicketCounts.GetValueOrDefault(type.Id),
            SlaValues = slaValues
                .Where(value => value.TicketTypeId == type.Id)
                .Select(ToSlaValueDto)
                .ToArray()
        }).ToArray();
    }

    public async Task<TicketTypeDto> CreateTicketTypeAsync(CreateTicketTypeRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        await EnsureTicketTypeNameUniqueAsync(name, null, ct);
        var code = NormalizeCode(request.Code, "type");
        await EnsureTicketTypeCodeUniqueAsync(code, null, ct);

        var type = new TicketType
        {
            TenantId = db.CurrentTenantId,
            Code = code,
            Name = name,
            IsBuiltIn = false,
            IsPortalAvailable = request.IsPortalAvailable,
            IsSlaEnabled = request.IsSlaEnabled
        };
        db.TicketTypes.Add(type);
        await db.SaveChangesAsync(ct);
        return (await GetTicketTypesAsync(ct)).Single(item => item.Id == type.Id);
    }

    public async Task<TicketTypeDto> UpdateTicketTypeAsync(int id, UpdateTicketTypeRequest request, CancellationToken ct = default)
    {
        var type = await db.TicketTypes.FirstOrDefaultAsync(item => item.Id == id && !item.IsArchived, ct)
            ?? throw new KeyNotFoundException("Тип заявки не найден.");

        var name = request.Name.Trim();
        await EnsureTicketTypeNameUniqueAsync(name, id, ct);
        type.Name = name;
        type.IsPortalAvailable = request.IsPortalAvailable;
        if (request.IsSlaEnabled is bool slaEnabled)
            type.IsSlaEnabled = slaEnabled;
        await db.SaveChangesAsync(ct);
        return (await GetTicketTypesAsync(ct)).Single(item => item.Id == id);
    }

    public async Task DeleteTicketTypeAsync(int id, CancellationToken ct = default) =>
        await ArchiveTicketTypeAsync(id, ct);

    public async Task<SlaSettingsDto> GetSlaSettingsAsync(CancellationToken ct = default)
    {
        var tenant = await GetTenantAsync(ct);
        return new SlaSettingsDto { IsEnabled = tenant.SlaEnabled };
    }

    public async Task<SlaSettingsDto> UpdateSlaSettingsAsync(SlaSettingsDto request, CancellationToken ct = default)
    {
        var tenant = await GetTenantAsync(ct);
        tenant.SlaEnabled = request.IsEnabled;
        await db.SaveChangesAsync(ct);
        return new SlaSettingsDto { IsEnabled = tenant.SlaEnabled, ActiveWorkingScheduleId = request.ActiveWorkingScheduleId };
    }

    public async Task<PortalSlaVisibilityDto> GetPortalSlaVisibilityAsync(CancellationToken ct = default)
    {
        var tenant = await GetTenantAsync(ct);
        return new PortalSlaVisibilityDto
        {
            ShowReactionTime = tenant.PortalSlaReactionVisible,
            ShowResolutionTime = tenant.PortalSlaResolutionVisible
        };
    }

    public async Task<PortalSlaVisibilityDto> UpdatePortalSlaVisibilityAsync(PortalSlaVisibilityDto request, CancellationToken ct = default)
    {
        var tenant = await GetTenantAsync(ct);
        tenant.PortalSlaReactionVisible = request.ShowReactionTime;
        tenant.PortalSlaResolutionVisible = request.ShowResolutionTime;
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task SetTicketTypeSlaEnabledAsync(int ticketTypeId, bool enabled, CancellationToken ct = default)
    {
        var type = await db.TicketTypes.FirstOrDefaultAsync(item => item.Id == ticketTypeId && !item.IsArchived, ct)
            ?? throw new KeyNotFoundException("Тип заявки не найден.");
        type.IsSlaEnabled = enabled;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SlaValueDto>> GetSlaValuesAsync(int? ticketTypeId, CancellationToken ct = default)
    {
        var typeIds = await GetActiveTicketTypeIdsAsync(ct);
        if (ticketTypeId is int requestedTypeId)
        {
            if (!typeIds.Contains(requestedTypeId))
                throw new KeyNotFoundException("Тип заявки не найден.");
            typeIds = [requestedTypeId];
        }

        return await db.SlaValues
            .AsNoTracking()
            .Where(value => typeIds.Contains(value.TicketTypeId))
            .OrderBy(value => value.TicketTypeId)
            .ThenBy(value => value.CriticalityLevelId)
            .Select(value => new SlaValueDto
            {
                TicketTypeId = value.TicketTypeId,
                CriticalityLevelId = value.CriticalityLevelId,
                ReactionTimeMinutes = value.ReactionTimeMinutes,
                ResolutionTimeMinutes = value.ResolutionTimeMinutes,
                IsDisabled = value.IsDisabled,
                UpdatedAt = value.UpdatedAt
            })
            .ToArrayAsync(ct);
    }

    public async Task<SlaValueDto> UpsertSlaValueAsync(SaveSlaValueRequest request, CancellationToken ct = default)
    {
        await EnsureTicketTypeExistsAsync(request.TicketTypeId, ct);
        await EnsureCriticalityExistsAsync(request.CriticalityLevelId, ct);
        var value = await UpsertSlaValueAsync(
            request.TicketTypeId,
            request.CriticalityLevelId,
            request.ReactionTimeMinutes,
            request.ResolutionTimeMinutes,
            request.IsDisabled,
            ct);
        await db.SaveChangesAsync(ct);
        return ToSlaValueDto(value);
    }

    public async Task<IReadOnlyList<SlaValueDto>> SaveTicketTypeSlaValuesAsync(
        int ticketTypeId,
        IReadOnlyList<TicketTypeSlaValueItem> items,
        CancellationToken ct = default)
    {
        await EnsureTicketTypeExistsAsync(ticketTypeId, ct);
        foreach (var item in items)
        {
            var criticalityId = await ResolveCriticalityIdAsync(item, ct);
            if (item.ReactionTimeMinutes is < 0 || item.ResolutionTimeMinutes is < 0)
                throw new ArgumentException("Длительности SLA не могут быть отрицательными.");
            await UpsertSlaValueAsync(
                ticketTypeId,
                criticalityId,
                item.ReactionTimeMinutes,
                item.ResolutionTimeMinutes,
                item.IsDisabled,
                ct);
        }

        await db.SaveChangesAsync(ct);
        return await GetSlaValuesAsync(ticketTypeId, ct);
    }

    private async Task ApplyCriticalitiesAsync(IReadOnlyList<CriticalitySettingsItem> items, CancellationToken ct)
    {
        var levels = await db.CriticalityLevels.ToArrayAsync(ct);
        var requestedIds = items.Where(item => item.Id > 0).Select(item => item.Id).ToHashSet();
        foreach (var level in levels.Where(level => !requestedIds.Contains(level.Id)))
        {
            var assignedTickets = await db.Tickets.CountAsync(ticket => ticket.CriticalityLevelId == level.Id, ct);
            if (assignedTickets > 0)
                throw new InvalidOperationException(
                    $"Уровень «{level.Name}» назначен хотя бы одной заявке и не может быть удалён.");
            var slaValues = await db.SlaValues.Where(value => value.CriticalityLevelId == level.Id).ToArrayAsync(ct);
            db.SlaValues.RemoveRange(slaValues);
            db.CriticalityLevels.Remove(level);
        }

        await db.SaveChangesAsync(ct);

        foreach (var item in items)
        {
            if (item.Id > 0)
            {
                var level = levels.FirstOrDefault(level => level.Id == item.Id)
                    ?? throw new KeyNotFoundException($"Уровень критичности {item.Id} не найден.");
                await EnsureCriticalityNameUniqueAsync(item.Name, item.Id, ct);
                level.Name = item.Name.Trim();
                level.SortOrder = item.SortOrder;
                level.IsActive = item.IsActive;
                continue;
            }

            await EnsureCriticalityNameUniqueAsync(item.Name, null, ct);
            var code = NormalizeCode(item.Code ?? item.Name, "level");
            await EnsureCriticalityCodeUniqueAsync(code, null, ct);
            db.CriticalityLevels.Add(new CriticalityLevel
            {
                TenantId = db.CurrentTenantId,
                Code = code,
                Name = item.Name.Trim(),
                SortOrder = item.SortOrder,
                IsActive = item.IsActive,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyTicketTypesAsync(IReadOnlyList<TicketTypeSettingsItem> items, CancellationToken ct)
    {
        var types = await db.TicketTypes.Where(type => !type.IsArchived).ToArrayAsync(ct);
        var requestedIds = items.Where(item => item.Id > 0).Select(item => item.Id).ToHashSet();
        foreach (var type in types.Where(type => !requestedIds.Contains(type.Id) && !type.IsBuiltIn))
            await ArchiveTicketTypeAsync(type.Id, ct, saveChanges: false);

        await db.SaveChangesAsync(ct);

        foreach (var item in items)
        {
            TicketType type;
            if (item.Id > 0)
            {
                type = types.FirstOrDefault(type => type.Id == item.Id)
                    ?? await db.TicketTypes.FirstOrDefaultAsync(type => type.Id == item.Id && type.IsArchived, ct)
                    ?? throw new KeyNotFoundException($"Тип заявки {item.Id} не найден.");
                type.IsArchived = false;
                await EnsureTicketTypeNameUniqueAsync(item.Name, type.Id, ct);
                type.Name = item.Name.Trim();
                type.IsPortalAvailable = item.IsPortalAvailable;
                type.IsSlaEnabled = item.IsSlaEnabled;
            }
            else
            {
                await EnsureTicketTypeNameUniqueAsync(item.Name, null, ct);
                var code = NormalizeCode(item.Code ?? item.Name, "type");
                await EnsureTicketTypeCodeUniqueAsync(code, null, ct);
                type = new TicketType
                {
                    TenantId = db.CurrentTenantId,
                    Code = code,
                    Name = item.Name.Trim(),
                    IsPortalAvailable = item.IsPortalAvailable,
                    IsSlaEnabled = item.IsSlaEnabled
                };
                db.TicketTypes.Add(type);
                await db.SaveChangesAsync(ct);
            }

            if (item.SlaValues is not { } slaValues)
                continue;

            foreach (var slaItem in slaValues)
            {
                if (slaItem.ReactionTimeMinutes is < 0 || slaItem.ResolutionTimeMinutes is < 0)
                    throw new ArgumentException("Длительности SLA не могут быть отрицательными.");
                var criticalityId = await ResolveCriticalityIdAsync(slaItem, ct);
                await UpsertSlaValueAsync(
                    type.Id,
                    criticalityId,
                    slaItem.ReactionTimeMinutes,
                    slaItem.ResolutionTimeMinutes,
                    slaItem.IsDisabled,
                    ct);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ArchiveTicketTypeAsync(int id, CancellationToken ct = default, bool saveChanges = true)
    {
        var type = await db.TicketTypes.FirstOrDefaultAsync(item => item.Id == id && !item.IsArchived, ct)
            ?? throw new KeyNotFoundException("Тип заявки не найден.");
        if (type.IsBuiltIn)
            throw new InvalidOperationException("Встроенный тип заявки нельзя удалить.");

        var closedStatusId = await db.TicketStatuses
            .Where(status => status.Code == ClosedStatusCode)
            .Select(status => (int?)status.Id)
            .FirstOrDefaultAsync(ct);
        var activeTickets = await db.Tickets
            .CountAsync(ticket => ticket.TypeId == id && (closedStatusId == null || ticket.StatusId != closedStatusId), ct);
        if (activeTickets > 0)
            throw new InvalidOperationException(
                $"Тип «{type.Name}» используется в активных заявках и не может быть удалён.");

        type.IsArchived = true;
        if (saveChanges)
            await db.SaveChangesAsync(ct);
    }

    private async Task<SlaValue> UpsertSlaValueAsync(
        int ticketTypeId,
        int criticalityLevelId,
        int? reactionTimeMinutes,
        int? resolutionTimeMinutes,
        bool isDisabled,
        CancellationToken ct)
    {
        var value = await db.SlaValues.FirstOrDefaultAsync(
            item => item.TicketTypeId == ticketTypeId && item.CriticalityLevelId == criticalityLevelId,
            ct);
        if (value is null)
        {
            value = new SlaValue { TicketTypeId = ticketTypeId, CriticalityLevelId = criticalityLevelId };
            db.SlaValues.Add(value);
        }

        value.ReactionTimeMinutes = reactionTimeMinutes;
        value.ResolutionTimeMinutes = resolutionTimeMinutes;
        value.IsDisabled = isDisabled;
        value.UpdatedAt = DateTimeOffset.UtcNow;
        return value;
    }

    private async Task<int> ResolveCriticalityIdAsync(TicketTypeSlaValueItem item, CancellationToken ct)
    {
        if (item.CriticalityLevelId > 0)
        {
            await EnsureCriticalityExistsAsync(item.CriticalityLevelId, ct);
            return item.CriticalityLevelId;
        }

        if (string.IsNullOrWhiteSpace(item.CriticalityName))
            throw new ArgumentException("Для настройки SLA необходимо указать уровень критичности.");

        var name = item.CriticalityName.Trim().ToLowerInvariant();
        var id = await db.CriticalityLevels
            .Where(level => level.Name.ToLower() == name)
            .Select(level => (int?)level.Id)
            .FirstOrDefaultAsync(ct);
        return id ?? throw new KeyNotFoundException($"Уровень критичности «{item.CriticalityName}» не найден.");
    }

    private async Task<IReadOnlyList<CriticalityLevelDto>> LoadCriticalitiesAsync(CancellationToken ct)
    {
        var levels = await db.CriticalityLevels
            .AsNoTracking()
            .OrderBy(level => level.SortOrder)
            .ThenBy(level => level.Id)
            .ToArrayAsync(ct);

        var usage = await db.Tickets
            .Where(ticket => ticket.CriticalityLevelId != null)
            .GroupBy(ticket => ticket.CriticalityLevelId!.Value)
            .Select(group => new { CriticalityLevelId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CriticalityLevelId, item => item.Count, ct);

        return levels.Select(level => new CriticalityLevelDto
        {
            Id = level.Id,
            Code = level.Code,
            Name = level.Name,
            SortOrder = level.SortOrder,
            IsActive = level.IsActive,
            AssignedTicketCount = usage.GetValueOrDefault(level.Id)
        }).ToArray();
    }

    private void ValidateCriticalities(IReadOnlyList<CriticalitySettingsItem> items)
    {
        var active = items.Count(item => item.IsActive);
        if (active < MinCriticalityLevels || active > MaxCriticalityLevels)
            throw new ArgumentException(
                $"Активных уровней критичности должно быть от {MinCriticalityLevels} до {MaxCriticalityLevels}.");

        var names = items.Select(item => item.Name.Trim()).ToArray();
        if (names.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Название уровня критичности не может быть пустым.");
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
            throw new ArgumentException("Названия уровней критичности должны быть уникальными.");
    }

    private static void ValidatePasswordPolicy(UpdatePasswordPolicyRequest request)
    {
        if (request.MinLength < 8)
            throw new ArgumentException("Минимальная длина пароля не может быть меньше 8.");
        if (request.MaxFailedAttempts < 1)
            throw new ArgumentException("Число неудачных попыток должно быть положительным.");
        if (request.LockoutMinutes < 1)
            throw new ArgumentException("Время блокировки должно быть положительным.");
        if (request.ExpirationDays is <= 0)
            throw new ArgumentException("Срок действия пароля должен быть положительным.");
    }

    private static void CopyPasswordPolicy(UpdatePasswordPolicyRequest source, PasswordPolicy target)
    {
        target.MinLength = source.MinLength;
        target.RequireDigit = source.RequireDigit;
        target.RequireUppercase = source.RequireUppercase;
        target.RequireLowercase = source.RequireLowercase;
        target.RequireSpecial = source.RequireSpecial;
        target.ExpirationDays = source.ExpirationDays;
        target.MaxFailedAttempts = source.MaxFailedAttempts;
        target.LockoutMinutes = source.LockoutMinutes;
    }

    private static PasswordPolicyDto ToPasswordPolicyDto(PasswordPolicy? policy) => new()
    {
        MinLength = policy?.MinLength ?? 8,
        RequireDigit = policy?.RequireDigit ?? true,
        RequireUppercase = policy?.RequireUppercase ?? false,
        RequireLowercase = policy?.RequireLowercase ?? true,
        RequireSpecial = policy?.RequireSpecial ?? false,
        ExpirationDays = policy?.ExpirationDays,
        MaxFailedAttempts = policy?.MaxFailedAttempts ?? 5,
        LockoutMinutes = policy?.LockoutMinutes ?? 15,
        UpdatedAt = policy?.UpdatedAt ?? DateTimeOffset.UtcNow
    };

    private static SlaValueDto ToSlaValueDto(SlaValue value) => new()
    {
        TicketTypeId = value.TicketTypeId,
        CriticalityLevelId = value.CriticalityLevelId,
        ReactionTimeMinutes = value.ReactionTimeMinutes,
        ResolutionTimeMinutes = value.ResolutionTimeMinutes,
        IsDisabled = value.IsDisabled,
        UpdatedAt = value.UpdatedAt
    };

    private async Task<Tenant> GetTenantAsync(CancellationToken ct) =>
        await db.Tenants.FirstOrDefaultAsync(tenant => tenant.Id == db.CurrentTenantId, ct)
        ?? throw new KeyNotFoundException("Текущий тенант не найден.");

    private async Task<int[]> GetActiveTicketTypeIdsAsync(CancellationToken ct) =>
        await db.TicketTypes.Where(type => !type.IsArchived).Select(type => type.Id).ToArrayAsync(ct);

    private async Task EnsureTicketTypeExistsAsync(int ticketTypeId, CancellationToken ct)
    {
        var exists = await db.TicketTypes.AnyAsync(type => type.Id == ticketTypeId && !type.IsArchived, ct);
        if (!exists)
            throw new KeyNotFoundException("Тип заявки не найден.");
    }

    private async Task EnsureCriticalityExistsAsync(int criticalityLevelId, CancellationToken ct)
    {
        var exists = await db.CriticalityLevels.AnyAsync(level => level.Id == criticalityLevelId, ct);
        if (!exists)
            throw new KeyNotFoundException("Уровень критичности не найден.");
    }

    private async Task EnsureCriticalityNameUniqueAsync(string name, int? excludedId, CancellationToken ct)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var exists = await db.CriticalityLevels.AnyAsync(
            level => level.Name.ToLower() == normalized && (excludedId == null || level.Id != excludedId),
            ct);
        if (exists)
            throw new InvalidOperationException($"Уровень критичности «{name.Trim()}» уже существует.");
    }

    private async Task EnsureCriticalityCodeUniqueAsync(string code, int? excludedId, CancellationToken ct)
    {
        var exists = await db.CriticalityLevels.AnyAsync(
            level => level.Code == code && (excludedId == null || level.Id != excludedId),
            ct);
        if (exists)
            throw new InvalidOperationException($"Код уровня критичности «{code}» уже используется.");
    }

    private async Task EnsureTicketTypeNameUniqueAsync(string name, int? excludedId, CancellationToken ct)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var exists = await db.TicketTypes.AnyAsync(
            type => !type.IsArchived
                && type.Name.ToLower() == normalized
                && (excludedId == null || type.Id != excludedId),
            ct);
        if (exists)
            throw new InvalidOperationException($"Тип заявки «{name.Trim()}» уже существует.");
    }

    private async Task EnsureTicketTypeCodeUniqueAsync(string code, int? excludedId, CancellationToken ct)
    {
        var exists = await db.TicketTypes.AnyAsync(
            type => type.Code == code && (excludedId == null || type.Id != excludedId),
            ct);
        if (exists)
            throw new InvalidOperationException($"Код типа заявки «{code}» уже используется.");
    }

    private static string NormalizeCode(string value, string prefix)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        var chars = trimmed
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray();
        var code = new string(chars).Trim('_');
        if (code.Length < 3)
            code = $"{prefix}_{Guid.NewGuid():N}"[..(prefix.Length + 9)];
        return code.Length <= 64 ? code : code[..64];
    }
}
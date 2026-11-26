using itsm.Application.Common.Models;

namespace itsm.Application.Common.Interfaces;

public interface IServiceDeskSettingsService
{
    Task<ServiceDeskSettingsDto> GetAsync(bool canManageAdminCreation, CancellationToken ct = default);
    Task<ServiceDeskSettingsDto> UpdateAsync(UpdateServiceDeskSettingsRequest request, bool canManageAdminCreation, CancellationToken ct = default);

    Task<PasswordPolicyDto> GetPasswordPolicyAsync(CancellationToken ct = default);
    Task<PasswordPolicyDto> UpdatePasswordPolicyAsync(UpdatePasswordPolicyRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<CriticalityLevelDto>> GetCriticalityLevelsAsync(CancellationToken ct = default);
    Task<CriticalityLevelDto> CreateCriticalityLevelAsync(CreateCriticalityLevelRequest request, CancellationToken ct = default);
    Task<CriticalityLevelDto> UpdateCriticalityLevelAsync(int id, UpdateCriticalityLevelRequest request, CancellationToken ct = default);
    Task DeleteCriticalityLevelAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<TicketTypeDto>> GetTicketTypesAsync(CancellationToken ct = default);
    Task<TicketTypeDto> CreateTicketTypeAsync(CreateTicketTypeRequest request, CancellationToken ct = default);
    Task<TicketTypeDto> UpdateTicketTypeAsync(int id, UpdateTicketTypeRequest request, CancellationToken ct = default);
    Task DeleteTicketTypeAsync(int id, CancellationToken ct = default);

    Task<SlaSettingsDto> GetSlaSettingsAsync(CancellationToken ct = default);
    Task<SlaSettingsDto> UpdateSlaSettingsAsync(SlaSettingsDto request, CancellationToken ct = default);
    Task<PortalSlaVisibilityDto> GetPortalSlaVisibilityAsync(CancellationToken ct = default);
    Task<PortalSlaVisibilityDto> UpdatePortalSlaVisibilityAsync(PortalSlaVisibilityDto request, CancellationToken ct = default);
    Task SetTicketTypeSlaEnabledAsync(int ticketTypeId, bool enabled, CancellationToken ct = default);
    Task<IReadOnlyList<SlaValueDto>> GetSlaValuesAsync(int? ticketTypeId, CancellationToken ct = default);
    Task<SlaValueDto> UpsertSlaValueAsync(SaveSlaValueRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<SlaValueDto>> SaveTicketTypeSlaValuesAsync(int ticketTypeId, IReadOnlyList<TicketTypeSlaValueItem> items, CancellationToken ct = default);
}
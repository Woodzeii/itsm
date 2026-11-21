using itsm.Application.Common.Models;

namespace itsm.Application.Common.Interfaces;

public interface IFormTemplateService
{
    Task<IReadOnlyList<TicketTypeOptionResponse>> GetTicketTypesAsync(CancellationToken ct = default);
    Task<TicketFormTemplateResponse?> GetFormTemplateAsync(int ticketTypeId, CancellationToken ct = default);
}

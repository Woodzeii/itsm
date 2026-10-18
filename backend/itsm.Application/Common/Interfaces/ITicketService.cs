using itsm.Application.Common.Models;

namespace itsm.Application.Common.Interfaces;

public interface ITicketService
{
    Task<List<TicketDto>> GetAllAsync(int? typeId, int? assigneeId, int? statusId, int? creatorId, CancellationToken ct = default);
    Task<TicketDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<TicketDto> CreateAsync(CreateTicketRequest request, int creatorId, CancellationToken ct = default);
    Task<TicketDto?> UpdateAsync(int id, UpdateTicketRequest request, int performedById, CancellationToken ct = default);
    Task<TicketDto?> ChangeStatusAsync(int id, ChangeTicketStatusRequest request, int performedById, CancellationToken ct = default);
    Task<TicketEscalationDto?> EscalateAsync(int id, EscalateTicketRequest request, int initiatorId, CancellationToken ct = default);
    Task<List<TicketMessageDto>> GetMessagesAsync(int ticketId, bool includeInternal, CancellationToken ct = default);
    Task<TicketMessageDto?> AddMessageAsync(int ticketId, CreateTicketMessageRequest request, int authorId, CancellationToken ct = default);
}

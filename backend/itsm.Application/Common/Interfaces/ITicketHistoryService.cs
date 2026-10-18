namespace itsm.Application.Common.Interfaces;

public interface ITicketHistoryService
{
    Task LogAsync(
        int ticketId,
        int userId,
        string fieldName,
        string? oldValue,
        string? newValue,
        CancellationToken ct = default);
}

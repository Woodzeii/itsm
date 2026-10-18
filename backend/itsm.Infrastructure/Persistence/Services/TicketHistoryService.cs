using itsm.Application.Common.Interfaces;
using itsm.Domain.Entities;

namespace itsm.Infrastructure.Persistence.Services;

public class TicketHistoryService : ITicketHistoryService
{
    private readonly ItsmDbContext _db;

    public TicketHistoryService(ItsmDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(
        int ticketId,
        int userId,
        string fieldName,
        string? oldValue,
        string? newValue,
        CancellationToken ct = default)
    {
        _db.TicketAuditLogs.Add(new TicketAuditLog
        {
            TicketId = ticketId,
            UserId = userId,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedAt = DateTimeOffset.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
    }
}

using itsm.Application.Common.Interfaces;
using itsm.Domain.Entities;

namespace itsm.Infrastructure.Persistence.Services;

/// <summary>
/// Записывает события в историю по активу (АКТ-15).
/// </summary>
public class AssetHistoryService : IAssetHistoryService
{
    private readonly ItsmDbContext _db;

    public AssetHistoryService(ItsmDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(
        int assetId,
        string actionType,
        string description,
        int? userId = null,
        string? details = null,
        int? ticketId = null,
        string? comment = null,
        CancellationToken ct = default)
    {
        var log = new AssetHistoryLog
        {
            AssetId = assetId,
            UserId = userId,
            ActionType = actionType,
            Comment = description,
            Details = details,
            TicketId = ticketId,
            ChangedAt = DateTimeOffset.UtcNow,
        };

        if (!string.IsNullOrEmpty(comment))
        {
            log.Comment = string.IsNullOrEmpty(log.Comment)
                ? comment
                : $"{log.Comment}. {comment}";
        }

        _db.AssetHistoryLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }
}

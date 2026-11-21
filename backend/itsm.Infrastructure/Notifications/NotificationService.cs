using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Notifications;

public sealed class NotificationService(ItsmDbContext db) : INotificationService
{
    public async Task<IReadOnlyList<NotificationResponse>> GetAsync(int userId, int skip = 0, int take = 30, CancellationToken ct = default)
    {
        var unreadCount = await GetUnreadCountAsync(userId, ct);
        var items = await db.Notifications.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Skip(skip).Take(take)
            .Select(item => new NotificationResponse(item.Id, item.Type, item.Title, item.Body, item.TicketId, item.IsRead, item.CreatedAt, unreadCount))
            .ToArrayAsync(ct);
        return items;
    }

    public async Task<NotificationResponse?> MarkReadAsync(int id, int userId, CancellationToken ct = default)
    {
        var item = await db.Notifications.FirstOrDefaultAsync(notification => notification.Id == id && notification.UserId == userId, ct);
        if (item is null) return null;
        item.IsRead = true;
        await db.SaveChangesAsync(ct);
        var unreadCount = await GetUnreadCountAsync(userId, ct);
        return new NotificationResponse(item.Id, item.Type, item.Title, item.Body, item.TicketId, true, item.CreatedAt, unreadCount);
    }

    public async Task<int> MarkAllReadAsync(int userId, CancellationToken ct = default)
    {
        var items = await db.Notifications.Where(item => item.UserId == userId && !item.IsRead).ToListAsync(ct);
        foreach (var item in items) item.IsRead = true;
        if (items.Count > 0) await db.SaveChangesAsync(ct);
        return items.Count;
    }

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default) =>
        db.Notifications.CountAsync(item => item.UserId == userId && !item.IsRead, ct);

    public async Task CreateNotification(int userId, string type, string message, int? ticketId, CancellationToken ct = default)
    {
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
        await db.SaveChangesAsync(ct);
    }
}

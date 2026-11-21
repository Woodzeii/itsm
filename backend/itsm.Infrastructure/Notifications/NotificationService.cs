using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Notifications;

public sealed class NotificationService(ItsmDbContext db) : INotificationService
{
    public async Task<IReadOnlyList<NotificationResponse>> GetAsync(
        int userId,
        int skip = 0,
        int take = 30,
        CancellationToken ct = default)
    {
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(take));

        var unreadCount = await GetUnreadCountAsync(userId, ct);
        var notifications = await db.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Skip(skip)
            .Take(take)
            .Select(notification => new NotificationResponse(
                notification.Id,
                notification.Type,
                notification.Title,
                notification.Body,
                notification.TicketId,
                notification.IsRead,
                notification.CreatedAt,
                unreadCount))
            .ToArrayAsync(ct);

        return notifications;
    }

    public async Task<NotificationResponse?> MarkReadAsync(int id, int userId, CancellationToken ct = default)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId, ct);
        if (notification is null) return null;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await db.SaveChangesAsync(ct);
        }

        var unreadCount = await GetUnreadCountAsync(userId, ct);
        return new NotificationResponse(
            notification.Id,
            notification.Type,
            notification.Title,
            notification.Body,
            notification.TicketId,
            notification.IsRead,
            notification.CreatedAt,
            unreadCount);
    }

    public async Task<int> MarkAllReadAsync(int userId, CancellationToken ct = default)
    {
        var unread = await db.Notifications
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .ToListAsync(ct);
        foreach (var notification in unread)
            notification.IsRead = true;

        if (unread.Count > 0)
            await db.SaveChangesAsync(ct);

        return unread.Count;
    }

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default) =>
        db.Notifications.CountAsync(notification => notification.UserId == userId && !notification.IsRead, ct);

    public async Task CreateNotification(
        int userId,
        string type,
        string message,
        int? ticketId,
        CancellationToken ct = default)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Некорректные параметры уведомления.");

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

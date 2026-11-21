namespace itsm.Application.Common.Interfaces;

using itsm.Application.Common.Models;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationResponse>> GetAsync(int userId, int skip = 0, int take = 30, CancellationToken ct = default);
    Task<NotificationResponse?> MarkReadAsync(int id, int userId, CancellationToken ct = default);
    Task<int> MarkAllReadAsync(int userId, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default);
    Task CreateNotification(int userId, string type, string message, int? ticketId, CancellationToken ct = default);
}

namespace itsm.Domain.Entities;

public class Tenant
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Лимиты на использование
    public int MaxUsers { get; set; } = 100;
    public int MaxAssets { get; set; } = 1000;
    public int MaxTickets { get; set; } = 10000;
    public long MaxStorageBytes { get; set; } = 10_737_418_240; // 10 GB

    // Блокировки и ограничения
    public bool IsBlocked { get; set; } = false;
    public string? BlockReason { get; set; }
    public DateTimeOffset? BlockedAt { get; set; }
    public DateTimeOffset? BlockedUntil { get; set; }

    // Статистика
    public int CurrentUserCount { get; set; } = 0;
    public int CurrentAssetCount { get; set; } = 0;
    public int CurrentTicketCount { get; set; } = 0;
    public long CurrentStorageBytes { get; set; } = 0;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Dictionary> Dictionaries { get; set; } = new List<Dictionary>();
}
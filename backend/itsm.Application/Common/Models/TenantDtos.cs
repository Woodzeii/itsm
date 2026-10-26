namespace itsm.Application.Common.Models;

public class TenantDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Лимиты
    public int MaxUsers { get; set; }
    public int MaxAssets { get; set; }
    public int MaxTickets { get; set; }
    public long MaxStorageBytes { get; set; }

    // Блокировки
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
    public DateTimeOffset? BlockedAt { get; set; }
    public DateTimeOffset? BlockedUntil { get; set; }

    // Статистика использования
    public int CurrentUserCount { get; set; }
    public int CurrentAssetCount { get; set; }
    public int CurrentTicketCount { get; set; }
    public long CurrentStorageBytes { get; set; }

    // Вычисляемые свойства для UI
    public decimal UsersPercentage => MaxUsers > 0 ? (decimal)CurrentUserCount / MaxUsers * 100 : 0;
    public decimal AssetsPercentage => MaxAssets > 0 ? (decimal)CurrentAssetCount / MaxAssets * 100 : 0;
    public decimal TicketsPercentage => MaxTickets > 0 ? (decimal)CurrentTicketCount / MaxTickets * 100 : 0;
    public decimal StoragePercentage => MaxStorageBytes > 0 ? (decimal)CurrentStorageBytes / MaxStorageBytes * 100 : 0;
}

public class CreateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string FirstAdminLogin { get; set; } = string.Empty;
    public int? MaxUsers { get; set; }
    public int? MaxAssets { get; set; }
    public int? MaxTickets { get; set; }
    public long? MaxStorageBytes { get; set; }
}

public class UpdateTenantRequest
{
    public string? Name { get; set; }
    public int? MaxUsers { get; set; }
    public int? MaxAssets { get; set; }
    public int? MaxTickets { get; set; }
    public long? MaxStorageBytes { get; set; }
}

public class UpdateTenantStatusRequest
{
    public bool IsActive { get; set; }
}

public class BlockTenantRequest
{
    public string? Reason { get; set; }
    public DateTimeOffset? BlockedUntil { get; set; }
}

public class UnblockTenantRequest
{
    public string? Note { get; set; }
}

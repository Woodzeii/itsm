namespace itsm.Domain.Entities;

public class Asset
{
    public int Id { get; set; }
    public string InventoryNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? SerialNumber { get; set; }
    public string? Status { get; set; }
    public DateOnly? LicenseExpirationDate { get; set; }
    public int? AssignedUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? AssignedUser { get; set; }
    public ICollection<AssetHistoryLog> HistoryLogs { get; set; } = new List<AssetHistoryLog>();
    public ICollection<TicketAssetMapping> TicketMappings { get; set; } = new List<TicketAssetMapping>();
}
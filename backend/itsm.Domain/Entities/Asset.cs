namespace itsm.Domain.Entities;

public class Asset
{
    public int Id { get; set; }
    public string InventoryNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? AssetClassId { get; set; }
    public string? Category { get; set; }
    public string? SerialNumber { get; set; }
    public string? Status { get; set; }
    public string LifecycleStage { get; set; } = "Purchased";
    public DateTimeOffset? LicenseExpirationDate { get; set; }
    public int? AssignedUserId { get; set; }
    public int? WarehouseId { get; set; }
    public int? LocationId { get; set; }
    public int? DepartmentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? AssignedUser { get; set; }
    public AssetClass? AssetClass { get; set; }
    public ICollection<AssetHistoryLog> HistoryLogs { get; set; } = new List<AssetHistoryLog>();
    public ICollection<AssetMovement> Movements { get; set; } = new List<AssetMovement>();
    public ICollection<TicketAssetMapping> TicketMappings { get; set; } = new List<TicketAssetMapping>();
}
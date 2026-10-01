namespace itsm.Domain.Entities;
public class AssetMovement
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public int? FromUserId { get; set; }
    public int? ToUserId { get; set; }
    public int? FromWarehouseId { get; set; }
    public int? ToWarehouseId { get; set; }
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }
    public int? FromDepartmentId { get; set; }
    public int? ToDepartmentId { get; set; }
    public int PerformedById { get; set; }
    public DateTimeOffset PerformedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Comment { get; set; }

    public Asset Asset { get; set; } = null!;
    public User? FromUser { get; set; }
    public User? ToUser { get; set; }
    public User PerformedBy { get; set; } = null!;
}
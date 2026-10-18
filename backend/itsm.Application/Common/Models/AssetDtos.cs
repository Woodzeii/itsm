namespace itsm.Application.Common.Models;

// ==================== Asset ====================

public class AssetDto
{
    public int Id { get; set; }
    public string? InventoryNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? AssetClassId { get; set; }
    public string? AssetClassName { get; set; }
    public string LifecycleStage { get; set; } = "Purchased";
    public int? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
    public int? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public int? LocationId { get; set; }
    public string? LocationName { get; set; }
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public Dictionary<string, object?> CustomAttributes { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
}

public class AssetDetailDto : AssetDto { }

public class CreateAssetRequest
{
    public string? InventoryNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public int AssetClassId { get; set; }
    public int? WarehouseId { get; set; }
    public Dictionary<string, object?>? CustomAttributes { get; set; }
}

public class UpdateAssetRequest
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object?>? CustomAttributes { get; set; }
}

// ==================== Movement ====================

public class AssetMovementDto
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public int? FromUserId { get; set; }
    public string? FromUserName { get; set; }
    public int? ToUserId { get; set; }
    public string? ToUserName { get; set; }
    public int? FromWarehouseId { get; set; }
    public int? ToWarehouseId { get; set; }
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }
    public int? FromDepartmentId { get; set; }
    public int? ToDepartmentId { get; set; }
    public int PerformedById { get; set; }
    public string PerformedByName { get; set; } = string.Empty;
    public DateTimeOffset PerformedAt { get; set; }
    public string? Comment { get; set; }
}

public class RegisterAssetMovementRequest
{
    public string MovementType { get; set; } = string.Empty;
    public int? FromUserId { get; set; }
    public int? ToUserId { get; set; }
    public int? FromWarehouseId { get; set; }
    public int? ToWarehouseId { get; set; }
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }
    public int? FromDepartmentId { get; set; }
    public int? ToDepartmentId { get; set; }
    public string? Comment { get; set; }
}

// ==================== History ====================

public class AssetHistoryItemDto
{
    public int Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public string? Details { get; set; }
    public int? TicketId { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

// ==================== TicketHistory ====================

public class AssetTicketHistoryItemDto
{
    public int Id { get; set; }
    public int TypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CreatorId { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public int? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}

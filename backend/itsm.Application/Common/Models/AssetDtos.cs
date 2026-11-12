namespace itsm.Application.Common.Models;

/// <summary>Актив в списке и в карточке.</summary>
public sealed record AssetDto(
    int Id,
    string? InventoryNumber,
    string? SerialNumber,
    string Name,
    int? AssetClassId,
    string? AssetClassName,
    string? Category,
    string LifecycleStage,
    string LifecycleStageName,
    int? AssignedUserId,
    string? AssignedUserName,
    int? WarehouseId,
    string? WarehouseName,
    int? LocationId,
    string? LocationName,
    int? DepartmentId,
    string? DepartmentName,
    DateTimeOffset? LicenseExpirationDate,
    DateTimeOffset CreatedAt);

/// <summary>Карточка актива с историей, движениями и связанными заявками.</summary>
public sealed record AssetDetailDto(
    AssetDto Asset,
    IReadOnlyList<AssetHistoryItemDto> History,
    IReadOnlyList<AssetMovementDto> Movements,
    IReadOnlyList<AssetTicketHistoryItem> RelatedTickets);

/// <summary>Запрос на создание актива.</summary>
public sealed record CreateAssetRequest
{
    public string? InventoryNumber { get; init; }
    public string? SerialNumber { get; init; }
    public required string Name { get; init; }
    public required int AssetClassId { get; init; }
    public string? Category { get; init; }
    public int? WarehouseId { get; init; }
    public int? LocationId { get; init; }
    public int? DepartmentId { get; init; }
    public int? AssignedUserId { get; init; }
    public string? LifecycleStage { get; init; }
    public DateTimeOffset? LicenseExpirationDate { get; init; }
}

/// <summary>Запрос на изменение карточки актива. Незаполненные поля не меняются.</summary>
public sealed record UpdateAssetRequest
{
    public string? InventoryNumber { get; init; }
    public string? SerialNumber { get; init; }
    public string? Name { get; init; }
    public int? AssetClassId { get; init; }
    public string? Category { get; init; }
    public int? WarehouseId { get; init; }
    public int? LocationId { get; init; }
    public int? DepartmentId { get; init; }
    public DateTimeOffset? LicenseExpirationDate { get; init; }
}

/// <summary>Движение актива.</summary>
public sealed record AssetMovementDto(
    int Id,
    int AssetId,
    string MovementType,
    string MovementTypeName,
    int? FromUserId,
    string? FromUserName,
    int? ToUserId,
    string? ToUserName,
    int? FromWarehouseId,
    int? ToWarehouseId,
    int? FromLocationId,
    int? ToLocationId,
    int? FromDepartmentId,
    int? ToDepartmentId,
    int PerformedById,
    string PerformedByName,
    DateTimeOffset PerformedAt,
    string? Comment);

/// <summary>Запрос на регистрацию движения актива.</summary>
public sealed record RegisterAssetMovementRequest
{
    public required string MovementType { get; init; }
    public int? ToUserId { get; init; }
    public int? ToWarehouseId { get; init; }
    public int? ToLocationId { get; init; }
    public int? ToDepartmentId { get; init; }
    public string? Comment { get; init; }
}

/// <summary>Запись журнала изменений актива.</summary>
public sealed record AssetHistoryItemDto(
    int Id,
    string ActionType,
    string ActionTypeName,
    string PerformedBy,
    string? Details,
    int? TicketId,
    string? Comment,
    DateTimeOffset CreatedAt);

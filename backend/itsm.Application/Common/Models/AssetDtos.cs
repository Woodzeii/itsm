namespace itsm.Application.Common.Models;

public sealed class FieldValidationException(string field, string message) : ArgumentException(message, field)
{
    public string Field { get; } = field;
}

/// <summary>Актив в списке и в карточке.</summary>
public sealed record AssetDto(
    int Id,
    string? InventoryNumber,
    string? SerialNumber,
    string Name,
    string? AssetClassCode,
    string? AssetClassName,
    string? Category,
    string LifecycleStage,
    string LifecycleStageName,
    int? AssignedUserId,
    string? AssignedUserName,
    string? WarehouseCode,
    string? WarehouseName,
    string? LocationCode,
    string? LocationName,
    string? DepartmentCode,
    string? DepartmentName,
    DateTimeOffset? LicenseExpirationDate,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, string> Attributes);

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
    public required string AssetClassCode { get; init; }
    public string? Category { get; init; }
    public string? WarehouseCode { get; init; }
    public string? LocationCode { get; init; }
    public string? DepartmentCode { get; init; }
    public int? AssignedUserId { get; init; }
    public Dictionary<string, string>? Attributes { get; init; }
    public string? LifecycleStage { get; init; }
    public DateTimeOffset? LicenseExpirationDate { get; init; }
}

/// <summary>Запрос на изменение карточки актива. Незаполненные поля не меняются.</summary>
public sealed record UpdateAssetRequest
{
    public string? InventoryNumber { get; init; }
    public string? SerialNumber { get; init; }
    public string? Name { get; init; }
    public string? AssetClassCode { get; init; }
    public string? Category { get; init; }
    public string? WarehouseCode { get; init; }
    public string? LocationCode { get; init; }
    public string? DepartmentCode { get; init; }
    public DateTimeOffset? LicenseExpirationDate { get; init; }
    public Dictionary<string, string>? Attributes { get; init; }
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
    string? FromWarehouseCode,
    string? FromWarehouseName,
    string? ToWarehouseCode,
    string? ToWarehouseName,
    string? FromLocationCode,
    string? FromLocationName,
    string? ToLocationCode,
    string? ToLocationName,
    string? FromDepartmentCode,
    string? FromDepartmentName,
    string? ToDepartmentCode,
    string? ToDepartmentName,
    int PerformedById,
    string PerformedByName,
    DateTimeOffset PerformedAt,
    string? Comment);

/// <summary>Запрос на регистрацию движения актива.</summary>
public sealed record RegisterAssetMovementRequest
{
    public required string MovementType { get; init; }
    public int? ToUserId { get; init; }
    public string? ToWarehouseCode { get; init; }
    public string? ToLocationCode { get; init; }
    public string? ToDepartmentCode { get; init; }
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

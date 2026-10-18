using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Persistence.Services;

public class AssetService : IAssetService
{
    private const string StagePurchased = "Purchased";
    private const string StageInWarehouse = "InWarehouse";
    private const string StageInUse = "InUse";
    private const string StageInRepair = "InRepair";
    private const string StageRetired = "Retired";

    private readonly ItsmDbContext _db;
    private readonly IAssetHistoryService _history;

    public AssetService(ItsmDbContext db, IAssetHistoryService history)
    {
        _db = db;
        _history = history;
    }

    // ==================== GET ALL ====================

    public async Task<List<AssetDto>> GetAllAsync(int? classId, string? status, CancellationToken ct = default)
    {
        var query = _db.Assets
            .Include(a => a.AssetClass)
            .Include(a => a.AssignedUser)
            .AsQueryable();

        if (classId.HasValue)
            query = query.Where(a => a.AssetClassId == classId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.LifecycleStage == status);

        var assets = await query
            .OrderBy(a => a.Id)
            .ToListAsync(ct);

        return assets.Select(a => MapToDto(a)).ToList();
    }

    // ==================== GET BY ID ====================

    public async Task<AssetDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var asset = await _db.Assets
            .Include(a => a.AssetClass)
            .Include(a => a.AssignedUser)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (asset is null) return null;

        var dto = MapToDto(asset);
        return new AssetDetailDto
        {
            Id = dto.Id,
            InventoryNumber = dto.InventoryNumber,
            Name = dto.Name,
            AssetClassId = dto.AssetClassId,
            AssetClassName = dto.AssetClassName,
            LifecycleStage = dto.LifecycleStage,
            AssignedUserId = dto.AssignedUserId,
            AssignedUserName = dto.AssignedUserName,
            WarehouseId = dto.WarehouseId,
            WarehouseName = dto.WarehouseName,
            LocationId = dto.LocationId,
            LocationName = dto.LocationName,
            DepartmentId = dto.DepartmentId,
            DepartmentName = dto.DepartmentName,
            CustomAttributes = dto.CustomAttributes,
            CreatedAt = dto.CreatedAt,
        };
    }

    // ==================== CREATE ====================

    public async Task<AssetDto> CreateAsync(CreateAssetRequest request, int performedById, CancellationToken ct = default)
    {
        // Валидация: AssetClass обязателен (АКТ-05)
        if (request.AssetClassId <= 0)
            throw new InvalidOperationException("AssetClassId обязателен");

        var assetClass = await _db.AssetClasses.FirstOrDefaultAsync(c => c.Id == request.AssetClassId, ct);
        if (assetClass is null)
            throw new InvalidOperationException($"Класс актива #{request.AssetClassId} не найден");

        // АКТ-19: если InventoryNumber задан — склад обязателен и этап «На складе»
        var inventoryNumber = request.InventoryNumber?.Trim() ?? string.Empty;
        var hasInventory = !string.IsNullOrEmpty(inventoryNumber);

        if (hasInventory && (request.WarehouseId is null || request.WarehouseId <= 0))
            throw new InvalidOperationException("При указании InventoryNumber склад обязателен (АКТ-19)");

        var stage = hasInventory ? StageInWarehouse : StagePurchased;

        var asset = new Asset
        {
            InventoryNumber = inventoryNumber,
            Name = request.Name.Trim(),
            AssetClassId = request.AssetClassId,
            LifecycleStage = stage,
            WarehouseId = request.WarehouseId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.Assets.Add(asset);
        await _db.SaveChangesAsync(ct);

        // История: создание
        await _history.LogAsync(
            asset.Id,
            AssetHistoryActionTypes.EntityCreated,
            $"Создан актив «{asset.Name}» (этап: {stage})",
            userId: performedById,
            ct: ct);

        // Перечитаем с навигацией
        var created = await _db.Assets
            .Include(a => a.AssetClass)
            .Include(a => a.AssignedUser)
            .FirstAsync(a => a.Id == asset.Id, ct);

        return MapToDto(created);
    }

    // ==================== UPDATE ====================

    public async Task<AssetDto?> UpdateAsync(int id, UpdateAssetRequest request, int performedById, CancellationToken ct = default)
    {
        var asset = await _db.Assets
            .Include(a => a.AssetClass)
            .Include(a => a.AssignedUser)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (asset is null) return null;

        var oldName = asset.Name;
        asset.Name = request.Name.Trim();

        await _db.SaveChangesAsync(ct);

        // История: изменение атрибутов
        if (oldName != asset.Name)
        {
            await _history.LogAsync(
                asset.Id,
                AssetHistoryActionTypes.AttributeChanged,
                $"Изменено наименование: «{oldName}» → «{asset.Name}»",
                userId: performedById,
                details: $"{{\"field\":\"name\",\"old\":\"{oldName}\",\"new\":\"{asset.Name}\"}}",
                ct: ct);
        }

        return MapToDto(asset);
    }

    // ==================== DELETE ====================

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (asset is null) return false;

        // АКТ-14: удаление только на этапе «Закуплен»
        if (asset.LifecycleStage != StagePurchased)
            throw new InvalidOperationException(
                $"Удаление разрешено только на этапе «Закуплен» (текущий: {asset.LifecycleStage}). " +
                "На остальных этапах актив выводится через «Списан».");

        _db.Assets.Remove(asset);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ==================== MOVEMENTS (Часть 2) ====================

    public async Task<AssetMovementDto?> RegisterMovementAsync(
    int id,
    RegisterAssetMovementRequest request,
    int performedById,
    CancellationToken ct = default)
	{
		var asset = await _db.Assets
			.Include(a => a.AssetClass)
			.Include(a => a.AssignedUser)
			.FirstOrDefaultAsync(a => a.Id == id, ct);

		if (asset is null) return null;

		// Валидация вида движения
		var validTypes = new[] { "TransferToEmployee", "Relocation", "WarehouseIssue", "WarehouseReturn" };
		if (!validTypes.Contains(request.MovementType))
			throw new InvalidOperationException($"Недопустимый MovementType: {request.MovementType}");

		// Логика ЖЦ (АКТ-12, АКТ-13)
		switch (request.MovementType)
		{
			case "WarehouseIssue":
				if (asset.LifecycleStage != StageInWarehouse)
					throw new InvalidOperationException(
						$"«Выдача со склада» допустима только из «На складе» (текущий: {asset.LifecycleStage}).");
				if (request.ToUserId is null || request.ToUserId <= 0)
					throw new InvalidOperationException("Для «Выдача со склада» обязателен ToUserId (кому выдать).");
				asset.LifecycleStage = StageInUse;
				asset.AssignedUserId = request.ToUserId;
				asset.WarehouseId = null;
				break;

			case "WarehouseReturn":
				if (asset.LifecycleStage != StageInUse)
					throw new InvalidOperationException(
						$"«Возврат на склад» допустим только из «В эксплуатации» (текущий: {asset.LifecycleStage}).");
				if (request.ToWarehouseId is null || request.ToWarehouseId <= 0)
					throw new InvalidOperationException("Для «Возврат на склад» обязателен ToWarehouseId.");
				asset.LifecycleStage = StageInWarehouse;
				asset.AssignedUserId = null;
				asset.WarehouseId = request.ToWarehouseId;
				break;

			case "TransferToEmployee":
				if (asset.LifecycleStage != StageInUse)
					throw new InvalidOperationException(
						$"«Передача сотруднику» допустима только из «В эксплуатации» (текущий: {asset.LifecycleStage}).");
				if (request.ToUserId is null || request.ToUserId <= 0)
					throw new InvalidOperationException("Для «Передача сотруднику» обязателен ToUserId.");
				asset.AssignedUserId = request.ToUserId;
				break;

			case "Relocation":
				if (asset.LifecycleStage != StageInUse && asset.LifecycleStage != StageInWarehouse)
					throw new InvalidOperationException(
						$"«Перемещение» допустимо только из «В эксплуатации» или «На складе» (текущий: {asset.LifecycleStage}).");
				if (request.ToLocationId is not null)
					asset.LocationId = request.ToLocationId;
				if (request.ToDepartmentId is not null)
					asset.DepartmentId = request.ToDepartmentId;
				break;
		}

		// Запись движения
		var movement = new AssetMovement
		{
			AssetId = asset.Id,
			MovementType = request.MovementType,
			FromUserId = request.FromUserId,
			ToUserId = request.ToUserId,
			FromWarehouseId = request.FromWarehouseId,
			ToWarehouseId = request.ToWarehouseId,
			FromLocationId = request.FromLocationId,
			ToLocationId = request.ToLocationId,
			FromDepartmentId = request.FromDepartmentId,
			ToDepartmentId = request.ToDepartmentId,
			PerformedById = performedById,
			PerformedAt = DateTimeOffset.UtcNow,
			Comment = request.Comment,
		};

		_db.AssetMovements.Add(movement);
		await _db.SaveChangesAsync(ct);

		// История: движение
		await _history.LogAsync(
			asset.Id,
			AssetHistoryActionTypes.Movement,
			$"Движение: {request.MovementType}" + (string.IsNullOrEmpty(request.Comment) ? "" : $" ({request.Comment})"),
			userId: performedById,
			details: System.Text.Json.JsonSerializer.Serialize(new
			{
				movementType = request.MovementType,
				fromUserId = request.FromUserId,
				toUserId = request.ToUserId,
				fromWarehouseId = request.FromWarehouseId,
				toWarehouseId = request.ToWarehouseId,
				fromLocationId = request.FromLocationId,
				toLocationId = request.ToLocationId,
				fromDepartmentId = request.FromDepartmentId,
				toDepartmentId = request.ToDepartmentId,
			}),
			ct: ct);

		// История: смена этапа (если изменился)
		if (request.MovementType == "WarehouseIssue" || request.MovementType == "WarehouseReturn")
		{
			await _history.LogAsync(
				asset.Id,
				AssetHistoryActionTypes.StageChanged,
				$"Этап ЖЦ изменён: {request.MovementType}",
				userId: performedById,
				ct: ct);
		}

		// DTO
		var performedBy = await _db.Users.FirstOrDefaultAsync(u => u.Id == performedById, ct);
		return new AssetMovementDto
		{
			Id = movement.Id,
			AssetId = movement.AssetId,
			MovementType = movement.MovementType,
			FromUserId = movement.FromUserId,
			ToUserId = movement.ToUserId,
			FromWarehouseId = movement.FromWarehouseId,
			ToWarehouseId = movement.ToWarehouseId,
			FromLocationId = movement.FromLocationId,
			ToLocationId = movement.ToLocationId,
			FromDepartmentId = movement.FromDepartmentId,
			ToDepartmentId = movement.ToDepartmentId,
			PerformedById = movement.PerformedById,
			PerformedByName = performedBy?.FullName ?? string.Empty,
			PerformedAt = movement.PerformedAt,
			Comment = movement.Comment,
		};
	}

    // ==================== HISTORY ====================

    public async Task<List<AssetHistoryItemDto>> GetHistoryAsync(int id, CancellationToken ct = default)
    {
        var logs = await _db.AssetHistoryLogs
            .Include(l => l.User)
            .Where(l => l.AssetId == id)
            .OrderByDescending(l => l.ChangedAt)
            .ToListAsync(ct);

        return logs.Select(l => new AssetHistoryItemDto
        {
            Id = l.Id,
            ActionType = l.ActionType,
            Description = l.Comment ?? string.Empty,
            PerformedBy = l.User?.FullName ?? "Система",
            Details = l.Details,
            TicketId = l.TicketId,
            Comment = l.Comment,
            CreatedAt = l.ChangedAt,
        }).ToList();
    }

    // ==================== TICKETS ====================

    public async Task<List<AssetTicketHistoryItemDto>> GetTicketsAsync(int id, CancellationToken ct = default)
	{
		var tickets = await _db.Tickets
			.Include(t => t.Type)
			.Include(t => t.Status)
			.Include(t => t.Creator)
			.Include(t => t.Assignee)
			.Where(t => t.AssetId == id)
			.OrderByDescending(t => t.CreatedAt)
			.ToListAsync(ct);

		return tickets.Select(t => new AssetTicketHistoryItemDto
		{
			Id = t.Id,
			TypeId = t.TypeId,
			TypeName = t.Type?.Name ?? string.Empty,
			Title = t.Title,
			CreatorId = t.CreatorId,
			CreatorName = t.Creator?.FullName ?? string.Empty,
			AssigneeId = t.AssigneeId,
			AssigneeName = t.Assignee?.FullName,
			StatusId = t.StatusId,
			StatusName = t.Status?.Name ?? string.Empty,
			CreatedAt = t.CreatedAt,
			ClosedAt = t.Status?.Code == "closed" ? t.UpdatedAt : null,
		}).ToList();
	}

    // ==================== HELPERS ====================

    private static AssetDto MapToDto(Asset a) => new()
    {
        Id = a.Id,
        InventoryNumber = string.IsNullOrEmpty(a.InventoryNumber) ? null : a.InventoryNumber,
        Name = a.Name,
        AssetClassId = a.AssetClassId,
        AssetClassName = a.AssetClass?.Name,
        LifecycleStage = a.LifecycleStage,
        AssignedUserId = a.AssignedUserId,
        AssignedUserName = a.AssignedUser?.FullName,
        WarehouseId = a.WarehouseId,
        WarehouseName = null,   // TODO: резолвить из DictionaryValue
        LocationId = a.LocationId,
        LocationName = null,    // TODO: резолвить из DictionaryValue
        DepartmentId = a.DepartmentId,
        DepartmentName = null,  // TODO: резолвить из DictionaryValue
        CustomAttributes = new Dictionary<string, object?>(),
        CreatedAt = a.CreatedAt,
    };
}

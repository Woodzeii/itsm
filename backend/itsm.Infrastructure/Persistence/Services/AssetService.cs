using System.Data.Common;
using System.Text.Json;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Persistence.Services;

public sealed class AssetService(ItsmDbContext db) : IAssetService
{
    private const string ClosedStatusCode = "closed";

    public async Task<IReadOnlyList<AssetDto>> GetAllAsync(
        int? assetClassId,
        string? lifecycleStage,
        string? search,
        CancellationToken ct = default)
    {
        var query = db.Assets.AsNoTracking();

        if (assetClassId is int classId)
            query = query.Where(asset => asset.AssetClassId == classId);

        if (!string.IsNullOrWhiteSpace(lifecycleStage))
            query = query.Where(asset => asset.LifecycleStage == lifecycleStage);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(asset =>
                EF.Functions.ILike(asset.Name, term)
                || (asset.InventoryNumber != null && EF.Functions.ILike(asset.InventoryNumber, term))
                || (asset.SerialNumber != null && EF.Functions.ILike(asset.SerialNumber, term)));
        }

        var assets = await query
            .Include(asset => asset.AssetClass)
            .Include(asset => asset.AssignedUser)
            .OrderByDescending(asset => asset.Id)
            .ToListAsync(ct);

        return assets.Select(ToDto).ToArray();
    }

    public async Task<AssetDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var asset = await AssetQuery().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (asset is null)
            return null;

        return await BuildDetailAsync(asset, ct);
    }

    public async Task<AssetDto> CreateAsync(CreateAssetRequest request, int actorId, CancellationToken ct = default)
    {
        var assetClass = await db.AssetClasses
            .Where(item => item.Id == request.AssetClassId)
            .Select(item => new { item.Id, item.IsActive })
            .FirstOrDefaultAsync(ct)
            ?? throw new ArgumentException("Класс актива не найден.");
        if (!assetClass.IsActive)
            throw new ArgumentException("Класс актива деактивирован.");

        var inventoryNumber = NormalizeIdentifier(request.InventoryNumber);
        var serialNumber = NormalizeIdentifier(request.SerialNumber);
        if (inventoryNumber is null && serialNumber is null)
            throw new ArgumentException("Укажите инвентарный или серийный номер актива.");

        var stage = request.LifecycleStage ?? (request.WarehouseId is not null
            ? AssetLifecycleStages.InStock
            : AssetLifecycleStages.Purchased);

        await ValidateIdentifiersAreFreeAsync(inventoryNumber, serialNumber, exceptAssetId: null, ct);
        await ValidateAssignmentsAsync(request.WarehouseId, request.LocationId, request.DepartmentId, request.AssignedUserId, ct);

        var now = DateTimeOffset.UtcNow;
        var asset = new Asset
        {
            Name = request.Name.Trim(),
            InventoryNumber = inventoryNumber ?? string.Empty,
            SerialNumber = serialNumber,
            AssetClassId = assetClass.Id,
            Category = NormalizeIdentifier(request.Category),
            LifecycleStage = stage,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            DepartmentId = request.DepartmentId,
            AssignedUserId = request.AssignedUserId,
            LicenseExpirationDate = request.LicenseExpirationDate,
            TenantId = db.GetCurrentTenantId(),
            CreatedAt = now
        };

        db.Assets.Add(asset);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new InvalidOperationException("Актив с таким инвентарным или серийным номером уже существует.");
        }

        await LogAsync(
            asset.Id,
            actorId,
            AssetHistoryActionTypes.EntityCreated,
            $"Создан актив <{asset.Name}> (этап: {AssetLifecycleStages.GetRussianName(asset.LifecycleStage)})",
            JsonSerializer.Serialize(new
            {
                inventoryNumber = asset.InventoryNumber,
                serialNumber = asset.SerialNumber,
                lifecycleStage = asset.LifecycleStage
            }),
            ct);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var saved = await AssetQuery().FirstAsync(item => item.Id == asset.Id, ct);
        return ToDto(saved);
    }

    public async Task<AssetDto?> UpdateAsync(int id, UpdateAssetRequest request, int actorId, CancellationToken ct = default)
    {
        var asset = await AssetQuery().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (asset is null)
            return null;

        var changes = new List<(string Field, string? OldValue, string? NewValue)>();

        if (request.Name is not null && request.Name.Trim() != asset.Name)
        {
            changes.Add(("name", asset.Name, request.Name.Trim()));
            asset.Name = request.Name.Trim();
        }

        if (request.Category is not null && request.Category.Trim() != (asset.Category ?? string.Empty))
        {
            changes.Add(("category", asset.Category, request.Category.Trim()));
            asset.Category = NormalizeIdentifier(request.Category);
        }

        var inventoryNumber = request.InventoryNumber is null ? null : NormalizeIdentifier(request.InventoryNumber);
        var serialNumber = request.SerialNumber is null ? null : NormalizeIdentifier(request.SerialNumber);
        if (request.InventoryNumber is not null || request.SerialNumber is not null)
        {
            await ValidateIdentifiersAreFreeAsync(
                request.InventoryNumber is null ? null : inventoryNumber,
                request.SerialNumber is null ? null : serialNumber,
                exceptAssetId: asset.Id,
                ct);
        }

        if (request.InventoryNumber is not null && (inventoryNumber ?? string.Empty) != asset.InventoryNumber)
        {
            changes.Add(("inventoryNumber", asset.InventoryNumber, inventoryNumber));
            asset.InventoryNumber = inventoryNumber ?? string.Empty;
        }

        if (request.SerialNumber is not null && serialNumber != asset.SerialNumber)
        {
            changes.Add(("serialNumber", asset.SerialNumber, serialNumber));
            asset.SerialNumber = serialNumber;
        }

        if (request.AssetClassId is int classId && classId != asset.AssetClassId)
        {
            var assetClass = await db.AssetClasses
                .Where(item => item.Id == classId)
                .Select(item => new { item.Id, item.IsActive })
                .FirstOrDefaultAsync(ct)
                ?? throw new ArgumentException("Класс актива не найден.");
            if (!assetClass.IsActive)
                throw new ArgumentException("Класс актива деактивирован.");

            changes.Add(("assetClassId", asset.AssetClassId?.ToString(), assetClass.Id.ToString()));
            asset.AssetClassId = assetClass.Id;
        }

        await ValidateAssignmentsAsync(request.WarehouseId, request.LocationId, request.DepartmentId, null, ct);

        if (request.WarehouseId is int warehouseId && warehouseId != asset.WarehouseId)
        {
            changes.Add(("warehouseId", asset.WarehouseId?.ToString(), warehouseId.ToString()));
            asset.WarehouseId = warehouseId;
        }

        if (request.LocationId is int locationId && locationId != asset.LocationId)
        {
            changes.Add(("locationId", asset.LocationId?.ToString(), locationId.ToString()));
            asset.LocationId = locationId;
        }

        if (request.DepartmentId is int departmentId && departmentId != asset.DepartmentId)
        {
            changes.Add(("departmentId", asset.DepartmentId?.ToString(), departmentId.ToString()));
            asset.DepartmentId = departmentId;
        }

        if (request.LicenseExpirationDate is DateTimeOffset licenseExpiration
            && licenseExpiration != asset.LicenseExpirationDate)
        {
            changes.Add(("licenseExpirationDate", asset.LicenseExpirationDate?.ToString(), licenseExpiration.ToString()));
            asset.LicenseExpirationDate = licenseExpiration;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new InvalidOperationException("Актив с таким инвентарным или серийным номером уже существует.");
        }

        foreach (var change in changes)
        {
            await LogAsync(
                asset.Id,
                actorId,
                AssetHistoryActionTypes.AttributeChanged,
                $"Изменено поле «{change.Field}»: <{change.OldValue ?? "—"}> → <{change.NewValue ?? "—"}>",
                JsonSerializer.Serialize(new { field = change.Field, old = change.OldValue, @new = change.NewValue }),
                ct);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var saved = await AssetQuery().FirstAsync(item => item.Id == asset.Id, ct);
        return ToDto(saved);
    }

    public async Task<bool> DeleteAsync(int id, int actorId, CancellationToken ct = default)
    {
        var asset = await db.Assets.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (asset is null)
            return false;

        if (asset.LifecycleStage != AssetLifecycleStages.Purchased)
            throw new InvalidOperationException(
                $"Удалить актив можно только на этапе «{AssetLifecycleStages.GetRussianName(AssetLifecycleStages.Purchased)}» (текущий: {AssetLifecycleStages.GetRussianName(asset.LifecycleStage)}). " +
                "Чтобы списать актив, переведите его на этап «Списан».");

        if (await db.Tickets.AnyAsync(ticket => ticket.AssetId == asset.Id, ct))
            throw new InvalidOperationException("К активу привязаны заявки, удаление невозможно.");

        var logs = await db.AssetHistoryLogs.Where(log => log.AssetId == asset.Id).ToListAsync(ct);
        db.AssetHistoryLogs.RemoveRange(logs);

        var movements = await db.AssetMovements.Where(movement => movement.AssetId == asset.Id).ToListAsync(ct);
        db.AssetMovements.RemoveRange(movements);

        var mappings = await db.TicketAssetMappings.Where(mapping => mapping.AssetId == asset.Id).ToListAsync(ct);
        db.TicketAssetMappings.RemoveRange(mappings);

        db.Assets.Remove(asset);
        await db.SaveChangesAsync(ct);

        _ = actorId;
        return true;
    }

    public async Task<AssetMovementDto?> RegisterMovementAsync(
        int id,
        RegisterAssetMovementRequest request,
        int actorId,
        CancellationToken ct = default)
    {
        var asset = await AssetQuery().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (asset is null)
            return null;

        var now = DateTimeOffset.UtcNow;
        var movement = new AssetMovement
        {
            AssetId = asset.Id,
            MovementType = request.MovementType,
            FromUserId = asset.AssignedUserId,
            FromWarehouseId = asset.WarehouseId,
            FromLocationId = asset.LocationId,
            FromDepartmentId = asset.DepartmentId,
            ToUserId = request.ToUserId,
            ToWarehouseId = request.ToWarehouseId,
            ToLocationId = request.ToLocationId,
            ToDepartmentId = request.ToDepartmentId,
            PerformedById = actorId,
            PerformedAt = now,
            Comment = NormalizeIdentifier(request.Comment)
        };

        var oldStage = asset.LifecycleStage;
        switch (request.MovementType)
        {
            case AssetMovementTypes.Issue:
                if (asset.LifecycleStage != AssetLifecycleStages.InStock)
                    throw new InvalidOperationException(
                        $"Выдача со склада доступна только для актива на складе (текущий этап: {AssetLifecycleStages.GetRussianName(asset.LifecycleStage)}).");
                if (request.ToUserId is not int issuedTo || issuedTo <= 0)
                    throw new ArgumentException("Для выдачи со склада укажите сотрудника, которому выдается актив.");

                await ValidateUserIsActiveAsync(issuedTo, "Сотрудник для выдачи не найден.", ct);
                asset.LifecycleStage = AssetLifecycleStages.InUse;
                asset.AssignedUserId = issuedTo;
                asset.WarehouseId = null;
                break;

            case AssetMovementTypes.Return:
                if (asset.LifecycleStage != AssetLifecycleStages.InUse)
                    throw new InvalidOperationException(
                        $"Возврат на склад доступен только для актива в эксплуатации (текущий этап: {AssetLifecycleStages.GetRussianName(asset.LifecycleStage)}).");
                if (request.ToWarehouseId is not int returnWarehouse || returnWarehouse <= 0)
                    throw new ArgumentException("Для возврата на склад укажите склад.");

                await ValidateWarehouseAsync(returnWarehouse, ct);
                asset.LifecycleStage = AssetLifecycleStages.InStock;
                asset.AssignedUserId = null;
                asset.WarehouseId = returnWarehouse;
                break;

            case AssetMovementTypes.Transfer:
                if (asset.LifecycleStage != AssetLifecycleStages.InUse)
                    throw new InvalidOperationException(
                        $"Передача доступна только для актива в эксплуатации (текущий этап: {AssetLifecycleStages.GetRussianName(asset.LifecycleStage)}).");
                if (request.ToUserId is not int newOwner || newOwner <= 0)
                    throw new ArgumentException("Для передачи укажите нового ответственного.");

                await ValidateUserIsActiveAsync(newOwner, "Новый ответственный не найден.", ct);
                asset.AssignedUserId = newOwner;
                break;

            case AssetMovementTypes.Relocate:
                if (asset.LifecycleStage is not (AssetLifecycleStages.InUse or AssetLifecycleStages.InStock))
                    throw new InvalidOperationException(
                        $"Перемещение доступно только для актива в эксплуатации или на складе (текущий этап: {AssetLifecycleStages.GetRussianName(asset.LifecycleStage)}).");
                if (request.ToLocationId is null && request.ToDepartmentId is null)
                    throw new ArgumentException("Для перемещения укажите новое место или подразделение.");

                await ValidateAssignmentsAsync(null, request.ToLocationId, request.ToDepartmentId, null, ct);
                if (request.ToLocationId is int locationId)
                    asset.LocationId = locationId;
                if (request.ToDepartmentId is int departmentId)
                    asset.DepartmentId = departmentId;
                break;

            default:
                throw new ArgumentException($"Неизвестный тип движения актива: {request.MovementType}");
        }

        db.AssetMovements.Add(movement);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);

        await LogAsync(
            asset.Id,
            actorId,
            AssetHistoryActionTypes.Movement,
            $"Движение: {AssetMovementTypes.GetRussianName(movement.MovementType)}" +
            (string.IsNullOrWhiteSpace(movement.Comment) ? string.Empty : $" ({movement.Comment})"),
            JsonSerializer.Serialize(new
            {
                movementType = movement.MovementType,
                fromUserId = movement.FromUserId,
                toUserId = movement.ToUserId,
                fromWarehouseId = movement.FromWarehouseId,
                toWarehouseId = movement.ToWarehouseId,
                fromLocationId = movement.FromLocationId,
                toLocationId = movement.ToLocationId,
                fromDepartmentId = movement.FromDepartmentId,
                toDepartmentId = movement.ToDepartmentId
            }),
            ct);

        if (asset.LifecycleStage != oldStage)
        {
            await LogAsync(
                asset.Id,
                actorId,
                AssetHistoryActionTypes.StageChanged,
                $"Этап изменен: {AssetLifecycleStages.GetRussianName(oldStage)} → {AssetLifecycleStages.GetRussianName(asset.LifecycleStage)}",
                JsonSerializer.Serialize(new { old = oldStage, @new = asset.LifecycleStage }),
                ct);
        }

        if (movement.FromUserId != movement.ToUserId && movement.ToUserId is not null)
        {
            var newOwnerName = await UserNameAsync(movement.ToUserId.Value, ct);
            var oldOwnerName = movement.FromUserId is int previousOwner
                ? await UserNameAsync(previousOwner, ct)
                : null;

            await LogAsync(
                asset.Id,
                actorId,
                AssetHistoryActionTypes.AssignedChanged,
                $"Ответственный изменен: <{oldOwnerName ?? "—"}> → <{newOwnerName}>",
                JsonSerializer.Serialize(new { old = movement.FromUserId, @new = movement.ToUserId }),
                ct);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var performer = await UserNameAsync(actorId, ct);
        var fromUserName = movement.FromUserId is int fromUserIdForDto ? await UserNameAsync(fromUserIdForDto, ct) : null;
        var toUserName = movement.ToUserId is int toUserIdForDto ? await UserNameAsync(toUserIdForDto, ct) : null;
        return new AssetMovementDto(
            movement.Id,
            movement.AssetId,
            movement.MovementType,
            AssetMovementTypes.GetRussianName(movement.MovementType),
            movement.FromUserId,
            fromUserName,
            movement.ToUserId,
            toUserName,
            movement.FromWarehouseId,
            movement.ToWarehouseId,
            movement.FromLocationId,
            movement.ToLocationId,
            movement.FromDepartmentId,
            movement.ToDepartmentId,
            movement.PerformedById,
            performer,
            movement.PerformedAt,
            movement.Comment);
    }

    public async Task<IReadOnlyList<AssetMovementDto>?> GetMovementsAsync(int id, CancellationToken ct = default)
    {
        if (!await db.Assets.AnyAsync(asset => asset.Id == id, ct))
            return null;

        var movements = await db.AssetMovements
            .AsNoTracking()
            .Where(movement => movement.AssetId == id)
            .Include(movement => movement.FromUser)
            .Include(movement => movement.ToUser)
            .Include(movement => movement.PerformedBy)
            .OrderByDescending(movement => movement.PerformedAt)
            .ThenByDescending(movement => movement.Id)
            .ToListAsync(ct);

        return movements.Select(ToMovementDto).ToArray();
    }

    public async Task<IReadOnlyList<AssetHistoryItemDto>?> GetHistoryAsync(int id, CancellationToken ct = default)
    {
        if (!await db.Assets.AnyAsync(asset => asset.Id == id, ct))
            return null;

        var logs = await db.AssetHistoryLogs
            .AsNoTracking()
            .Where(log => log.AssetId == id)
            .Include(log => log.User)
            .OrderByDescending(log => log.ChangedAt)
            .ThenByDescending(log => log.Id)
            .ToListAsync(ct);

        return logs.Select(ToHistoryDto).ToArray();
    }

    public async Task<IReadOnlyList<AssetTicketHistoryItem>?> GetTicketsAsync(int id, CancellationToken ct = default)
    {
        if (!await db.Assets.AnyAsync(asset => asset.Id == id, ct))
            return null;

        var tickets = await db.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.AssetId == id)
            .Include(ticket => ticket.Type)
            .Include(ticket => ticket.Status)
            .Include(ticket => ticket.Creator)
            .Include(ticket => ticket.Requester)
            .Include(ticket => ticket.Assignee)
            .Include(ticket => ticket.CriticalityLevel)
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ToListAsync(ct);

        return tickets.Select(ticket => new AssetTicketHistoryItem(
            ticket.Id,
            ticket.TypeId,
            ticket.Type.Name,
            ticket.Title,
            ticket.CreatorId,
            ticket.Creator.FullName,
            ticket.RequesterId,
            ticket.Requester?.FullName,
            ticket.AssigneeId,
            ticket.Assignee?.FullName,
            ticket.StatusId,
            ticket.Status.Name,
            ticket.CriticalityLevelId,
            ticket.CriticalityLevel?.Name,
            ticket.CreatedAt,
            ticket.Status.Code == ClosedStatusCode ? ticket.UpdatedAt : null)).ToArray();
    }

    private IQueryable<Asset> AssetQuery() =>
        db.Assets
            .Include(asset => asset.AssetClass)
            .Include(asset => asset.AssignedUser);

    private async Task<AssetDetailDto> BuildDetailAsync(Asset asset, CancellationToken ct)
    {
        var history = await GetHistoryAsync(asset.Id, ct) ?? [];
        var movements = await GetMovementsAsync(asset.Id, ct) ?? [];
        var tickets = await GetTicketsAsync(asset.Id, ct) ?? [];

        return new AssetDetailDto(ToDto(asset), history, movements, tickets);
    }

    private async Task ValidateIdentifiersAreFreeAsync(
        string? inventoryNumber,
        string? serialNumber,
        int? exceptAssetId,
        CancellationToken ct)
    {
        if (inventoryNumber is not null)
        {
            var duplicateInventory = await db.Assets.AnyAsync(asset =>
                asset.InventoryNumber == inventoryNumber && asset.Id != exceptAssetId, ct);
            if (duplicateInventory)
                throw new InvalidOperationException($"Актив с инвентарным номером '{inventoryNumber}' уже существует.");
        }

        if (serialNumber is not null)
        {
            var duplicateSerial = await db.Assets.AnyAsync(asset =>
                asset.SerialNumber == serialNumber && asset.Id != exceptAssetId, ct);
            if (duplicateSerial)
                throw new InvalidOperationException($"Актив с серийным номером '{serialNumber}' уже существует.");
        }
    }

    private async Task ValidateAssignmentsAsync(
        int? warehouseId,
        int? locationId,
        int? departmentId,
        int? assignedUserId,
        CancellationToken ct)
    {
        if (warehouseId is int warehouse)
            await ValidateWarehouseAsync(warehouse, ct);

        if (locationId is int location)
            await ValidateDictionaryValueAsync("locations", location, "Место не найдено.", ct);

        if (departmentId is int department)
            await ValidateDictionaryValueAsync("departments", department, "Подразделение не найдено.", ct);

        if (assignedUserId is int userId)
            await ValidateUserIsActiveAsync(userId, "Ответственный сотрудник не найден.", ct);
    }

    private async Task ValidateWarehouseAsync(int warehouseId, CancellationToken ct) =>
        await ValidateDictionaryValueAsync("warehouses", warehouseId, "Склад не найден.", ct);

    private async Task ValidateDictionaryValueAsync(string dictionaryCode, int valueId, string error, CancellationToken ct)
    {
        var exists = await db.DictionaryValues
            .AnyAsync(value => value.Id == valueId
                && !value.IsArchived
                && value.Dictionary.Code == dictionaryCode, ct);
        if (!exists)
            throw new ArgumentException(error);
    }

    private async Task ValidateUserIsActiveAsync(int userId, string error, CancellationToken ct)
    {
        var exists = await db.Users.AnyAsync(user => user.Id == userId && user.IsActive, ct);
        if (!exists)
            throw new ArgumentException(error);
    }

    private async Task<string> UserNameAsync(int userId, CancellationToken ct) =>
        await db.Users.Where(user => user.Id == userId).Select(user => user.FullName).FirstOrDefaultAsync(ct)
        ?? string.Empty;

    private async Task LogAsync(
        int assetId,
        int actorId,
        string actionType,
        string description,
        string? details,
        CancellationToken ct)
    {
        db.AssetHistoryLogs.Add(new AssetHistoryLog
        {
            AssetId = assetId,
            UserId = actorId,
            ActionType = actionType,
            Comment = description,
            Details = details,
            ChangedAt = DateTimeOffset.UtcNow
        });

        await Task.CompletedTask;
    }

    private static string? NormalizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToUpperInvariant();
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is DbException dbException
        && dbException.SqlState == "23505";

    private static AssetDto ToDto(Asset asset) => new(
        asset.Id,
        string.IsNullOrEmpty(asset.InventoryNumber) ? null : asset.InventoryNumber,
        asset.SerialNumber,
        asset.Name,
        asset.AssetClassId,
        asset.AssetClass?.Name,
        asset.Category,
        asset.LifecycleStage,
        AssetLifecycleStages.GetRussianName(asset.LifecycleStage),
        asset.AssignedUserId,
        asset.AssignedUser?.FullName,
        asset.WarehouseId,
        null,
        asset.LocationId,
        null,
        asset.DepartmentId,
        null,
        asset.LicenseExpirationDate,
        asset.CreatedAt);

    private static AssetMovementDto ToMovementDto(AssetMovement movement) => new(
        movement.Id,
        movement.AssetId,
        movement.MovementType,
        AssetMovementTypes.GetRussianName(movement.MovementType),
        movement.FromUserId,
        movement.FromUser?.FullName,
        movement.ToUserId,
        movement.ToUser?.FullName,
        movement.FromWarehouseId,
        movement.ToWarehouseId,
        movement.FromLocationId,
        movement.ToLocationId,
        movement.FromDepartmentId,
        movement.ToDepartmentId,
        movement.PerformedById,
        movement.PerformedBy?.FullName ?? string.Empty,
        movement.PerformedAt,
        movement.Comment);

    private static AssetHistoryItemDto ToHistoryDto(AssetHistoryLog log) => new(
        log.Id,
        log.ActionType,
        GetActionTypeName(log.ActionType),
        log.User?.FullName ?? "Система",
        log.Details,
        log.TicketId,
        log.Comment,
        log.ChangedAt);

    private static string GetActionTypeName(string actionType) => actionType switch
    {
        AssetHistoryActionTypes.EntityCreated => "Создание актива",
        AssetHistoryActionTypes.AttributeChanged => "Изменение атрибутов",
        AssetHistoryActionTypes.Movement => "Движение актива",
        AssetHistoryActionTypes.StageChanged => "Смена этапа",
        AssetHistoryActionTypes.AssignedChanged => "Смена ответственного",
        AssetHistoryActionTypes.TicketCreated => "Создана заявка",
        AssetHistoryActionTypes.TicketClosed => "Закрыта заявка",
        _ => actionType
    };
}

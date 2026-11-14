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
        string? assetClassCode,
        string? lifecycleStage,
        string? search,
        CancellationToken ct = default)
    {
        var query = db.Assets.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(assetClassCode))
            query = query.Where(asset => asset.AssetClass != null && asset.AssetClass.Code == assetClassCode);

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

        return await ToDtosAsync(assets, ct);
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
        if (request.LifecycleStage is not null)
            throw new ArgumentException("Этап жизненного цикла назначается сервером.");
        if (request.AssignedUserId is not null)
            throw new ArgumentException("Сотрудника можно назначить только при выдаче актива со склада.");

        var assetClass = await db.AssetClasses
            .Where(item => item.Code == request.AssetClassCode)
            .Select(item => new { item.Id, item.IsActive })
            .FirstOrDefaultAsync(ct)
            ?? throw new FieldValidationException(nameof(request.AssetClassCode), "Класс актива не найден.");
        if (!assetClass.IsActive)
            throw new FieldValidationException(nameof(request.AssetClassCode), "Класс актива деактивирован.");

        var inventoryNumber = NormalizeIdentifier(request.InventoryNumber);
        var serialNumber = NormalizeIdentifier(request.SerialNumber);
        if (inventoryNumber is null && serialNumber is null)
            throw new ArgumentException("Укажите инвентарный или серийный номер актива.");
        var warehouseId = await ResolveActiveDictionaryValueIdAsync(
            "warehouses", request.WarehouseCode, nameof(request.WarehouseCode), ct);
        var locationId = await ResolveActiveDictionaryValueIdAsync(
            "locations", request.LocationCode, nameof(request.LocationCode), ct);
        var departmentId = await ResolveActiveDictionaryValueIdAsync(
            "departments", request.DepartmentCode, nameof(request.DepartmentCode), ct);

        if (warehouseId is not null && inventoryNumber is null)
            throw new ArgumentException("Для размещения актива на складе укажите инвентарный номер.");

        var stage = inventoryNumber is not null && warehouseId is not null
            ? AssetLifecycleStages.InStock
            : AssetLifecycleStages.Purchased;

        await ValidateIdentifiersAreFreeAsync(inventoryNumber, serialNumber, exceptAssetId: null, ct);
        if (request.AssignedUserId is int assignedUserId)
            await ValidateUserIsActiveAsync(assignedUserId, "Ответственный сотрудник не найден.", ct);

        var now = DateTimeOffset.UtcNow;
        var asset = new Asset
        {
            Name = request.Name.Trim(),
            InventoryNumber = inventoryNumber ?? string.Empty,
            SerialNumber = serialNumber,
            AssetClassId = assetClass.Id,
            Category = NormalizeIdentifier(request.Category),
            AttributesJson = SerializeAttributes(request.Attributes),
            LifecycleStage = stage,
            WarehouseId = warehouseId,
            LocationId = locationId,
            DepartmentId = departmentId,
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
        return await ToDtoAsync(saved, ct);
    }

    public async Task<AssetDto?> UpdateAsync(int id, UpdateAssetRequest request, int actorId, CancellationToken ct = default)
    {
        var asset = await AssetQuery().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (asset is null)
            return null;

        var changes = new List<(string Field, string? OldValue, string? NewValue)>();
        var resultingInventoryNumber = request.InventoryNumber is null
            ? asset.InventoryNumber
            : NormalizeIdentifier(request.InventoryNumber);
        var resultingSerialNumber = request.SerialNumber is null
            ? asset.SerialNumber
            : NormalizeIdentifier(request.SerialNumber);
        if (resultingInventoryNumber is null && resultingSerialNumber is null)
            throw new ArgumentException("У актива должен оставаться инвентарный или серийный номер.");
        if (asset.LifecycleStage != AssetLifecycleStages.Purchased && resultingInventoryNumber is null)
            throw new InvalidOperationException("Для актива после закупки необходим инвентарный номер.");

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

        if (request.AssetClassCode is string assetClassCode && assetClassCode != asset.AssetClass?.Code)
        {
            var assetClass = await db.AssetClasses
                .Where(item => item.Code == assetClassCode)
                .Select(item => new { item.Id, item.IsActive })
                .FirstOrDefaultAsync(ct)
                ?? throw new FieldValidationException(nameof(request.AssetClassCode), "Класс актива не найден.");
            if (!assetClass.IsActive)
                throw new FieldValidationException(nameof(request.AssetClassCode), "Класс актива деактивирован.");

            changes.Add(("assetClassCode", asset.AssetClass?.Code, assetClassCode));
            asset.AssetClassId = assetClass.Id;
        }

        var requestedWarehouseId = await ResolveUpdatedDictionaryValueIdAsync(
            "warehouses", request.WarehouseCode, asset.WarehouseId, nameof(request.WarehouseCode), ct);
        if (request.WarehouseCode is not null && requestedWarehouseId != asset.WarehouseId)
            throw new InvalidOperationException("Склад можно изменить только через движение актива.");

        var requestedLocationId = await ResolveUpdatedDictionaryValueIdAsync(
            "locations", request.LocationCode, asset.LocationId, nameof(request.LocationCode), ct);
        var requestedDepartmentId = await ResolveUpdatedDictionaryValueIdAsync(
            "departments", request.DepartmentCode, asset.DepartmentId, nameof(request.DepartmentCode), ct);

        if (request.LocationCode is not null && requestedLocationId != asset.LocationId)
        {
            var value = await GetDictionaryValueAsync(requestedLocationId!.Value, ct);
            changes.Add(("locationCode", await GetDictionaryValueCodeAsync(asset.LocationId, ct), value.Code));
            asset.LocationId = requestedLocationId;
        }

        if (request.DepartmentCode is not null && requestedDepartmentId != asset.DepartmentId)
        {
            var value = await GetDictionaryValueAsync(requestedDepartmentId!.Value, ct);
            changes.Add(("departmentCode", await GetDictionaryValueCodeAsync(asset.DepartmentId, ct), value.Code));
            asset.DepartmentId = requestedDepartmentId;
        }

        if (request.LicenseExpirationDate is DateTimeOffset licenseExpiration
            && licenseExpiration != asset.LicenseExpirationDate)
        {
            changes.Add(("licenseExpirationDate", asset.LicenseExpirationDate?.ToString(), licenseExpiration.ToString()));
            asset.LicenseExpirationDate = licenseExpiration;
        }

        if (request.Attributes is not null)
        {
            var oldAttributes = DeserializeAttributes(asset.AttributesJson);
            if (!AttributesEqual(oldAttributes, request.Attributes))
            {
                changes.Add(("attributes", JsonSerializer.Serialize(oldAttributes), JsonSerializer.Serialize(request.Attributes)));
                asset.AttributesJson = SerializeAttributes(request.Attributes);
            }
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
        return await ToDtoAsync(saved, ct);
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

        var toWarehouseId = await ResolveActiveDictionaryValueIdAsync(
            "warehouses", request.ToWarehouseCode, nameof(request.ToWarehouseCode), ct);
        var toLocationId = await ResolveActiveDictionaryValueIdAsync(
            "locations", request.ToLocationCode, nameof(request.ToLocationCode), ct);
        var toDepartmentId = await ResolveActiveDictionaryValueIdAsync(
            "departments", request.ToDepartmentCode, nameof(request.ToDepartmentCode), ct);

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
            ToWarehouseId = toWarehouseId,
            ToLocationId = toLocationId,
            ToDepartmentId = toDepartmentId,
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
                if (asset.WarehouseId is null)
                    throw new InvalidOperationException("Для выдачи актив должен быть привязан к складу.");
                if (request.ToUserId is not int issuedTo || issuedTo <= 0)
                    throw new ArgumentException("Для выдачи со склада укажите сотрудника, которому выдается актив.");
                if (request.ToWarehouseCode is not null || request.ToLocationCode is not null || request.ToDepartmentCode is not null)
                    throw new FieldValidationException(nameof(request.ToWarehouseCode), "Для выдачи укажите только сотрудника-получателя.");

                await ValidateUserIsActiveAsync(issuedTo, "Сотрудник для выдачи не найден.", ct);
                asset.LifecycleStage = AssetLifecycleStages.InUse;
                asset.AssignedUserId = issuedTo;
                asset.WarehouseId = null;
                break;

            case AssetMovementTypes.Return:
                if (asset.LifecycleStage != AssetLifecycleStages.InUse)
                    throw new InvalidOperationException(
                        $"Возврат на склад доступен только для актива в эксплуатации (текущий этап: {AssetLifecycleStages.GetRussianName(asset.LifecycleStage)}).");
                if (asset.AssignedUserId is null)
                    throw new InvalidOperationException("Для возврата актив должен быть закреплен за сотрудником.");
                if (toWarehouseId is not int returnWarehouse)
                    throw new FieldValidationException(nameof(request.ToWarehouseCode), "Для возврата на склад укажите склад.");
                if (request.ToUserId is not null || request.ToLocationCode is not null || request.ToDepartmentCode is not null)
                    throw new FieldValidationException(nameof(request.ToWarehouseCode), "Для возврата укажите только склад-получатель.");

                asset.LifecycleStage = AssetLifecycleStages.InStock;
                asset.AssignedUserId = null;
                asset.WarehouseId = returnWarehouse;
                break;

            case AssetMovementTypes.Relocate:
                if (asset.LifecycleStage != AssetLifecycleStages.InStock)
                    throw new InvalidOperationException(
                        $"Перемещение между складами доступно только для актива на складе (текущий этап: {AssetLifecycleStages.GetRussianName(asset.LifecycleStage)}).");
                if (asset.WarehouseId is null)
                    throw new InvalidOperationException("Для перемещения актив должен быть привязан к складу.");
                if (toWarehouseId is not int newWarehouse)
                    throw new FieldValidationException(nameof(request.ToWarehouseCode), "Для перемещения укажите склад назначения.");
                if (newWarehouse == asset.WarehouseId)
                    throw new FieldValidationException(nameof(request.ToWarehouseCode), "Склад назначения должен отличаться от текущего склада.");
                if (request.ToUserId is not null || request.ToLocationCode is not null || request.ToDepartmentCode is not null)
                    throw new FieldValidationException(nameof(request.ToWarehouseCode), "Для перемещения укажите только склад назначения.");

                asset.WarehouseId = newWarehouse;
                break;

            default:
                throw new ArgumentException($"Неизвестный тип движения актива: {request.MovementType}");
        }

        db.AssetMovements.Add(movement);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);

        var movementValues = await GetDictionaryValuesAsync(
            [
                movement.FromWarehouseId,
                movement.ToWarehouseId,
                movement.FromLocationId,
                movement.ToLocationId,
                movement.FromDepartmentId,
                movement.ToDepartmentId
            ],
            ct);
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
                fromWarehouseCode = ResolveDictionaryValue(movementValues, movement.FromWarehouseId, "warehouses")?.Code,
                toWarehouseCode = ResolveDictionaryValue(movementValues, movement.ToWarehouseId, "warehouses")?.Code,
                fromLocationCode = ResolveDictionaryValue(movementValues, movement.FromLocationId, "locations")?.Code,
                toLocationCode = ResolveDictionaryValue(movementValues, movement.ToLocationId, "locations")?.Code,
                fromDepartmentCode = ResolveDictionaryValue(movementValues, movement.FromDepartmentId, "departments")?.Code,
                toDepartmentCode = ResolveDictionaryValue(movementValues, movement.ToDepartmentId, "departments")?.Code
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

        var savedMovement = await db.AssetMovements
            .AsNoTracking()
            .Include(item => item.FromUser)
            .Include(item => item.ToUser)
            .Include(item => item.PerformedBy)
            .FirstAsync(item => item.Id == movement.Id, ct);
        return (await ToMovementDtosAsync([savedMovement], ct)).Single();
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

        return await ToMovementDtosAsync(movements, ct);
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
            ticket.TicketNumber,
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

        return new AssetDetailDto(await ToDtoAsync(asset, ct), history, movements, tickets);
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

    private async Task<int?> ResolveActiveDictionaryValueIdAsync(
        string dictionaryCode,
        string? valueCode,
        string field,
        CancellationToken ct)
    {
        if (valueCode is null)
            return null;

        var normalizedCode = valueCode.Trim();
        if (normalizedCode.Length == 0)
            throw new FieldValidationException(field, "Укажите код значения справочника.");

        var valueId = await db.DictionaryValues
            .Where(value =>
                value.Code == normalizedCode
                && value.Dictionary.Code == dictionaryCode
                && value.Dictionary.TenantId == db.GetCurrentTenantId()
                && !value.Dictionary.IsArchived
                && !value.IsArchived)
            .Select(value => (int?)value.Id)
            .FirstOrDefaultAsync(ct);

        return valueId
            ?? throw new FieldValidationException(
                field, $"Активное значение с кодом '{normalizedCode}' не найдено в справочнике '{dictionaryCode}'.");
    }

    private async Task<int?> ResolveUpdatedDictionaryValueIdAsync(
        string dictionaryCode,
        string? valueCode,
        int? currentValueId,
        string field,
        CancellationToken ct)
    {
        if (valueCode is null)
            return null;

        var currentCode = await GetDictionaryValueCodeAsync(currentValueId, ct);
        if (string.Equals(valueCode.Trim(), currentCode, StringComparison.Ordinal))
            return currentValueId;

        return await ResolveActiveDictionaryValueIdAsync(dictionaryCode, valueCode, field, ct);
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

    private static string? SerializeAttributes(IReadOnlyDictionary<string, string>? attributes) =>
        attributes is null ? null : JsonSerializer.Serialize(attributes);

    private static Dictionary<string, string> DeserializeAttributes(string? attributesJson) =>
        string.IsNullOrWhiteSpace(attributesJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(attributesJson)
                ?? new Dictionary<string, string>();

    private static bool AttributesEqual(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right) =>
        left.Count == right.Count
        && left.All(pair => right.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is DbException dbException
        && dbException.SqlState == "23505";

    private async Task<AssetDto> ToDtoAsync(Asset asset, CancellationToken ct)
    {
        var values = await GetDictionaryValuesAsync(
            [asset.WarehouseId, asset.LocationId, asset.DepartmentId], ct);
        return ToDto(asset, values);
    }

    private async Task<IReadOnlyList<AssetDto>> ToDtosAsync(IReadOnlyCollection<Asset> assets, CancellationToken ct)
    {
        var values = await GetDictionaryValuesAsync(
            assets.SelectMany(asset => new[] { asset.WarehouseId, asset.LocationId, asset.DepartmentId }), ct);
        return assets.Select(asset => ToDto(asset, values)).ToArray();
    }

    private async Task<IReadOnlyList<AssetMovementDto>> ToMovementDtosAsync(
        IReadOnlyCollection<AssetMovement> movements,
        CancellationToken ct)
    {
        var values = await GetDictionaryValuesAsync(
            movements.SelectMany(movement => new[]
            {
                movement.FromWarehouseId,
                movement.ToWarehouseId,
                movement.FromLocationId,
                movement.ToLocationId,
                movement.FromDepartmentId,
                movement.ToDepartmentId
            }), ct);

        return movements.Select(movement => ToMovementDto(movement, values)).ToArray();
    }

    private async Task<Dictionary<int, DictionaryValueReference>> GetDictionaryValuesAsync(
        IEnumerable<int?> valueIds,
        CancellationToken ct)
    {
        var ids = valueIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        var tenantId = db.GetCurrentTenantId();
        var values = await db.DictionaryValues
            .AsNoTracking()
            .Where(value => ids.Contains(value.Id) && value.Dictionary.TenantId == tenantId)
            .Select(value => new DictionaryValueReference(
                value.Id, value.Dictionary.Code, value.Code, value.Name))
            .ToListAsync(ct);

        return values.ToDictionary(value => value.Id);
    }

    private static AssetDto ToDto(Asset asset, IReadOnlyDictionary<int, DictionaryValueReference> values)
    {
        var warehouse = ResolveDictionaryValue(values, asset.WarehouseId, "warehouses");
        var location = ResolveDictionaryValue(values, asset.LocationId, "locations");
        var department = ResolveDictionaryValue(values, asset.DepartmentId, "departments");
        return new AssetDto(
            asset.Id,
            string.IsNullOrEmpty(asset.InventoryNumber) ? null : asset.InventoryNumber,
            asset.SerialNumber,
            asset.Name,
            asset.AssetClass?.Code,
            asset.AssetClass?.Name,
            asset.Category,
            asset.LifecycleStage,
            AssetLifecycleStages.GetRussianName(asset.LifecycleStage),
            asset.AssignedUserId,
            asset.AssignedUser?.FullName,
            warehouse?.Code,
            warehouse?.Name,
            location?.Code,
            location?.Name,
            department?.Code,
            department?.Name,
            asset.LicenseExpirationDate,
            asset.CreatedAt,
            DeserializeAttributes(asset.AttributesJson));
    }

    private static AssetMovementDto ToMovementDto(
        AssetMovement movement,
        IReadOnlyDictionary<int, DictionaryValueReference> values)
    {
        var fromWarehouse = ResolveDictionaryValue(values, movement.FromWarehouseId, "warehouses");
        var toWarehouse = ResolveDictionaryValue(values, movement.ToWarehouseId, "warehouses");
        var fromLocation = ResolveDictionaryValue(values, movement.FromLocationId, "locations");
        var toLocation = ResolveDictionaryValue(values, movement.ToLocationId, "locations");
        var fromDepartment = ResolveDictionaryValue(values, movement.FromDepartmentId, "departments");
        var toDepartment = ResolveDictionaryValue(values, movement.ToDepartmentId, "departments");
        return new AssetMovementDto(
            movement.Id,
            movement.AssetId,
            movement.MovementType,
            AssetMovementTypes.GetRussianName(movement.MovementType),
            movement.FromUserId,
            movement.FromUser?.FullName,
            movement.ToUserId,
            movement.ToUser?.FullName,
            fromWarehouse?.Code,
            fromWarehouse?.Name,
            toWarehouse?.Code,
            toWarehouse?.Name,
            fromLocation?.Code,
            fromLocation?.Name,
            toLocation?.Code,
            toLocation?.Name,
            fromDepartment?.Code,
            fromDepartment?.Name,
            toDepartment?.Code,
            toDepartment?.Name,
            movement.PerformedById,
            movement.PerformedBy?.FullName ?? string.Empty,
            movement.PerformedAt,
            movement.Comment);
    }

    private static DictionaryValueReference? ResolveDictionaryValue(
        IReadOnlyDictionary<int, DictionaryValueReference> values,
        int? valueId,
        string expectedDictionary)
    {
        if (valueId is int id
            && values.TryGetValue(id, out var value)
            && value.DictionaryCode == expectedDictionary)
            return value;
        return null;
    }

    private async Task<DictionaryValue> GetDictionaryValueAsync(int id, CancellationToken ct) =>
        await db.DictionaryValues.FirstAsync(value =>
            value.Id == id && value.Dictionary.TenantId == db.GetCurrentTenantId(), ct);

    private async Task<string?> GetDictionaryValueCodeAsync(int? id, CancellationToken ct) =>
        id is int valueId
            ? await db.DictionaryValues
                .Where(value => value.Id == valueId && value.Dictionary.TenantId == db.GetCurrentTenantId())
                .Select(value => value.Code)
                .FirstOrDefaultAsync(ct)
            : null;

    private sealed record DictionaryValueReference(int Id, string DictionaryCode, string Code, string Name);

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

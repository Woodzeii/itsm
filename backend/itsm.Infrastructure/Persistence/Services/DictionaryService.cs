using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Persistence.Services;

public class DictionaryService : IDictionaryService
{
    private readonly ItsmDbContext _db;

    public DictionaryService(ItsmDbContext db)
    {
        _db = db;
    }

    private int GetCurrentTenantId() => _db.GetCurrentTenantId();

    public async Task<List<DictionaryDto>> GetAllAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        var tenantId = GetCurrentTenantId();
        var query = _db.Dictionaries.Where(dictionary => dictionary.TenantId == tenantId);
        if (!includeArchived)
            query = query.Where(dictionary => !dictionary.IsArchived);

        return await query
            .OrderBy(dictionary => dictionary.Id)
            .Select(dictionary => new DictionaryDto
            {
                Code = dictionary.Code,
                Name = dictionary.Name,
                IsSystem = dictionary.IsSystem,
                IsArchived = dictionary.IsArchived,
                ValuesCount = dictionary.Values.Count(value => !value.IsArchived)
            })
            .ToListAsync(ct);
    }

    public async Task<DictionaryDetailDto?> GetByCodeAsync(
        string code,
        bool includeArchived = false,
        CancellationToken ct = default)
    {
        var tenantId = GetCurrentTenantId();
        var query = _db.Dictionaries
            .Where(dictionary => dictionary.Code == code && dictionary.TenantId == tenantId);
        if (!includeArchived)
            query = query.Where(dictionary => !dictionary.IsArchived);

        var dictionary = await query
            .Include(item => item.Values)
            .FirstOrDefaultAsync(ct);
        if (dictionary is null)
            return null;

        return new DictionaryDetailDto
        {
            Code = dictionary.Code,
            Name = dictionary.Name,
            IsSystem = dictionary.IsSystem,
            IsArchived = dictionary.IsArchived,
            Values = dictionary.Values
                .OrderBy(value => value.SortOrder)
                .ThenBy(value => value.Id)
                .Select(value => ToValueDto(dictionary.Code, value))
                .ToList()
        };
    }

    public async Task<DictionaryDto> CreateAsync(CreateDictionaryRequest request, CancellationToken ct = default)
    {
        var tenantId = GetCurrentTenantId();
        var code = request.Code.Trim();

        if (await _db.Dictionaries.AnyAsync(dictionary =>
                dictionary.TenantId == tenantId && dictionary.Code == code, ct))
            throw new InvalidOperationException($"Справочник с кодом '{code}' уже существует");

        var dictionary = new Dictionary
        {
            TenantId = tenantId,
            Code = code,
            Name = request.Name.Trim(),
            IsSystem = false,
            IsArchived = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Dictionaries.Add(dictionary);
        await _db.SaveChangesAsync(ct);

        return ToDictionaryDto(dictionary, 0);
    }

    public async Task<DictionaryDto?> UpdateAsync(
        string code,
        UpdateDictionaryRequest request,
        CancellationToken ct = default)
    {
        var tenantId = GetCurrentTenantId();
        var dictionary = await _db.Dictionaries
            .FirstOrDefaultAsync(item => item.Code == code && item.TenantId == tenantId, ct);
        if (dictionary is null)
            return null;

        dictionary.Name = request.Name.Trim();
        await _db.SaveChangesAsync(ct);

        var valuesCount = await _db.DictionaryValues
            .CountAsync(value => value.DictionaryId == dictionary.Id && !value.IsArchived, ct);
        return ToDictionaryDto(dictionary, valuesCount);
    }

    public async Task<bool> DeleteAsync(string code, CancellationToken ct = default)
    {
        var tenantId = GetCurrentTenantId();
        var dictionary = await _db.Dictionaries
            .Where(item => item.Code == code && item.TenantId == tenantId)
            .Include(item => item.Values)
            .FirstOrDefaultAsync(ct);
        if (dictionary is null)
            return false;

        if (dictionary.IsSystem)
            throw new InvalidOperationException("Системный справочник нельзя удалить");

        if (dictionary.Values.Count > 0)
            throw new InvalidOperationException("Нельзя удалить справочник со значениями. Сначала удалите значения.");

        if (await _db.FormFields.AnyAsync(field => field.DictionaryId == dictionary.Id, ct))
            throw new InvalidOperationException("Нельзя удалить справочник, который используется в шаблонах форм.");

        _db.Dictionaries.Remove(dictionary);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DictionaryDto?> ArchiveAsync(string code, CancellationToken ct = default)
    {
        var dictionary = await GetDictionaryAsync(code, ct);
        if (dictionary is null)
            return null;
        if (dictionary.IsSystem)
            throw new InvalidOperationException("Системный справочник нельзя архивировать.");

        dictionary.IsArchived = true;
        await _db.SaveChangesAsync(ct);
        var valuesCount = await _db.DictionaryValues
            .CountAsync(value => value.DictionaryId == dictionary.Id && !value.IsArchived, ct);
        return ToDictionaryDto(dictionary, valuesCount);
    }

    public async Task<DictionaryDto?> RestoreAsync(string code, CancellationToken ct = default)
    {
        var dictionary = await GetDictionaryAsync(code, ct);
        if (dictionary is null)
            return null;

        dictionary.IsArchived = false;
        await _db.SaveChangesAsync(ct);
        var valuesCount = await _db.DictionaryValues
            .CountAsync(value => value.DictionaryId == dictionary.Id && !value.IsArchived, ct);
        return ToDictionaryDto(dictionary, valuesCount);
    }

    public async Task<List<DictionaryValueDto>?> GetValuesAsync(
        string dictionaryCode,
        bool includeArchived = false,
        CancellationToken ct = default)
    {
        var dictionary = await GetDictionaryAsync(dictionaryCode, ct);
        if (dictionary is null || (!includeArchived && dictionary.IsArchived))
            return null;

        var query = _db.DictionaryValues.Where(value => value.DictionaryId == dictionary.Id);
        if (!includeArchived)
            query = query.Where(value => !value.IsArchived);

        return await query
            .OrderBy(value => value.SortOrder)
            .ThenBy(value => value.Id)
            .Select(value => ToValueDto(dictionary.Code, value))
            .ToListAsync(ct);
    }

    public async Task<DictionaryValueDto?> CreateValueAsync(
        string dictionaryCode,
        CreateDictionaryValueRequest request,
        CancellationToken ct = default)
    {
        var dictionary = await GetDictionaryAsync(dictionaryCode, ct);
        if (dictionary is null)
            return null;
        if (dictionary.IsArchived)
            throw new InvalidOperationException("Нельзя добавлять значения в архивный справочник.");

        var code = request.Code.Trim();
        if (await _db.DictionaryValues.AnyAsync(value =>
                value.DictionaryId == dictionary.Id && value.Code == code, ct))
            throw new InvalidOperationException($"Значение с кодом '{code}' уже существует в этом справочнике");

        var value = new DictionaryValue
        {
            DictionaryId = dictionary.Id,
            Code = code,
            Name = request.Name.Trim(),
            IsArchived = false,
            SortOrder = request.SortOrder,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.DictionaryValues.Add(value);
        await _db.SaveChangesAsync(ct);
        return ToValueDto(dictionary.Code, value);
    }

    public async Task<DictionaryValueDto?> UpdateValueAsync(
        string dictionaryCode,
        string valueCode,
        UpdateDictionaryValueRequest request,
        CancellationToken ct = default)
    {
        var dictionary = await GetDictionaryAsync(dictionaryCode, ct);
        if (dictionary is null)
            return null;

        var value = await _db.DictionaryValues
            .FirstOrDefaultAsync(item =>
                item.DictionaryId == dictionary.Id && item.Code == valueCode, ct);
        if (value is null)
            return null;

        value.Name = request.Name.Trim();
        value.SortOrder = request.SortOrder;
        await _db.SaveChangesAsync(ct);
        return ToValueDto(dictionary.Code, value);
    }

    public async Task<bool> DeleteValueAsync(string dictionaryCode, string valueCode, CancellationToken ct = default)
    {
        var value = await GetValueAsync(dictionaryCode, valueCode, ct);
        if (value is null)
            return false;

        if (!value.IsArchived)
            throw new InvalidOperationException("Нельзя удалить активное значение. Сначала архивируйте его.");

        await CheckIfValueIsUsedAsync(value, ct);
        _db.DictionaryValues.Remove(value);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DictionaryValueDto?> ArchiveValueAsync(
        string dictionaryCode,
        string valueCode,
        CancellationToken ct = default)
    {
        var value = await GetValueAsync(dictionaryCode, valueCode, ct);
        if (value is null)
            return null;

        value.IsArchived = true;
        await _db.SaveChangesAsync(ct);
        return ToValueDto(dictionaryCode, value);
    }

    public async Task<DictionaryValueDto?> RestoreValueAsync(
        string dictionaryCode,
        string valueCode,
        CancellationToken ct = default)
    {
        var dictionary = await GetDictionaryAsync(dictionaryCode, ct);
        if (dictionary is null)
            return null;
        if (dictionary.IsArchived)
            throw new InvalidOperationException("Нельзя восстановить значение в архивном справочнике.");

        var value = await _db.DictionaryValues
            .FirstOrDefaultAsync(item =>
                item.DictionaryId == dictionary.Id && item.Code == valueCode, ct);
        if (value is null)
            return null;

        value.IsArchived = false;
        await _db.SaveChangesAsync(ct);
        return ToValueDto(dictionary.Code, value);
    }

    private async Task<Dictionary?> GetDictionaryAsync(string code, CancellationToken ct)
    {
        var tenantId = GetCurrentTenantId();
        return await _db.Dictionaries
            .FirstOrDefaultAsync(dictionary => dictionary.Code == code && dictionary.TenantId == tenantId, ct);
    }

    private async Task<DictionaryValue?> GetValueAsync(string dictionaryCode, string valueCode, CancellationToken ct)
    {
        var tenantId = GetCurrentTenantId();
        return await _db.DictionaryValues
            .Where(value =>
                value.Code == valueCode
                && value.Dictionary.Code == dictionaryCode
                && value.Dictionary.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);
    }

    private async Task CheckIfValueIsUsedAsync(DictionaryValue value, CancellationToken ct)
    {
        var tenantId = GetCurrentTenantId();
        var dictionaryCode = await _db.Dictionaries
            .Where(dictionary => dictionary.Id == value.DictionaryId && dictionary.TenantId == tenantId)
            .Select(dictionary => dictionary.Code)
            .FirstOrDefaultAsync(ct);
        if (dictionaryCode is null)
            return;

        var isUsed = dictionaryCode switch
        {
            "asset_status" => await _db.Assets.AnyAsync(
                asset => asset.TenantId == tenantId && asset.Status == value.Code, ct),
            "asset_category" => await _db.Assets.AnyAsync(
                asset => asset.TenantId == tenantId && asset.Category == value.Code, ct),
            "warehouses" =>
                await _db.Assets.AnyAsync(
                    asset => asset.TenantId == tenantId && asset.WarehouseId == value.Id, ct)
                || await _db.AssetMovements.AnyAsync(
                    movement => movement.Asset.TenantId == tenantId
                        && (movement.FromWarehouseId == value.Id || movement.ToWarehouseId == value.Id), ct),
            "locations" =>
                await _db.Assets.AnyAsync(
                    asset => asset.TenantId == tenantId && asset.LocationId == value.Id, ct)
                || await _db.AssetMovements.AnyAsync(
                    movement => movement.Asset.TenantId == tenantId
                        && (movement.FromLocationId == value.Id || movement.ToLocationId == value.Id), ct),
            "departments" =>
                await _db.Assets.AnyAsync(
                    asset => asset.TenantId == tenantId && asset.DepartmentId == value.Id, ct)
                || await _db.AssetMovements.AnyAsync(
                    movement => movement.Asset.TenantId == tenantId
                        && (movement.FromDepartmentId == value.Id || movement.ToDepartmentId == value.Id), ct),
            _ => false
        };

        if (isUsed)
            throw new InvalidOperationException(
                $"Значение '{value.Name}' используется активами и не может быть удалено.");
    }

    private static DictionaryDto ToDictionaryDto(Dictionary dictionary, int valuesCount) => new()
    {
        Code = dictionary.Code,
        Name = dictionary.Name,
        IsSystem = dictionary.IsSystem,
        IsArchived = dictionary.IsArchived,
        ValuesCount = valuesCount
    };

    private static DictionaryValueDto ToValueDto(string dictionaryCode, DictionaryValue value) => new()
    {
        DictionaryCode = dictionaryCode,
        Code = value.Code,
        Name = value.Name,
        IsArchived = value.IsArchived,
        SortOrder = value.SortOrder
    };
}

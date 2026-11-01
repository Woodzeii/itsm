using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace itsm.Infrastructure.Persistence.Services;

public class DictionaryService : IDictionaryService
{
    private readonly ItsmDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DictionaryService(ItsmDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    private int GetCurrentTenantId()
    {
        var tenantIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId");
        if (int.TryParse(tenantIdClaim?.Value, out var tenantId))
            return tenantId;

        return 1;
    }

    public async Task<List<DictionaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = GetCurrentTenantId();
        
        return await _db.Dictionaries
            .Where(d => d.TenantId == tenantId)
            .OrderBy(d => d.Id)
            .Select(d => new DictionaryDto
            {
                Id = d.Id,
                Code = d.Code,
                Name = d.Name,
                IsSystem = d.IsSystem,
                ValuesCount = d.Values.Count(v => !v.IsArchived)
            })
            .ToListAsync(ct);
    }

    public async Task<DictionaryDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var tenantId = GetCurrentTenantId();
        
        var dict = await _db.Dictionaries
            .Where(d => d.Id == id && d.TenantId == tenantId)
            .Include(d => d.Values)
            .FirstOrDefaultAsync(ct);

        if (dict is null) return null;

        return new DictionaryDetailDto
        {
            Id = dict.Id,
            Code = dict.Code,
            Name = dict.Name,
            IsSystem = dict.IsSystem,
            Values = dict.Values
                .OrderBy(v => v.SortOrder).ThenBy(v => v.Id)
                .Select(ToValueDto)
                .ToList()
        };
    }

    public async Task<DictionaryDto> CreateAsync(CreateDictionaryRequest request, CancellationToken ct = default)
    {
        var tenantId = GetCurrentTenantId();
        
        if (await _db.Dictionaries.AnyAsync(d => d.TenantId == tenantId && d.Code == request.Code, ct))
            throw new InvalidOperationException($"Справочник с кодом '{request.Code}' уже существует");

        var dict = new Dictionary
        {
            TenantId = tenantId,
            Code = request.Code,
            Name = request.Name,
            IsSystem = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Dictionaries.Add(dict);
        await _db.SaveChangesAsync(ct);

        return new DictionaryDto
        {
            Id = dict.Id,
            Code = dict.Code,
            Name = dict.Name,
            IsSystem = dict.IsSystem,
            ValuesCount = 0
        };
    }

    public async Task<DictionaryDto?> UpdateAsync(int id, UpdateDictionaryRequest request, CancellationToken ct = default)
    {
        var tenantId = GetCurrentTenantId();
        
        var dict = await _db.Dictionaries
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId, ct);
        if (dict is null) return null;

        dict.Name = request.Name;
        await _db.SaveChangesAsync(ct);

        return new DictionaryDto
        {
            Id = dict.Id,
            Code = dict.Code,
            Name = dict.Name,
            IsSystem = dict.IsSystem,
            ValuesCount = await _db.DictionaryValues.CountAsync(v => v.DictionaryId == id && !v.IsArchived, ct)
        };
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var tenantId = GetCurrentTenantId();
        
        var dict = await _db.Dictionaries
            .Where(d => d.Id == id && d.TenantId == tenantId)
            .Include(d => d.Values)
            .FirstOrDefaultAsync(ct);

        if (dict is null) return false;

        if (dict.IsSystem)
            throw new InvalidOperationException("Системный справочник нельзя удалить");

        if (dict.Values.Any())
            throw new InvalidOperationException("Нельзя удалить справочник со значениями. Сначала удалите значения.");

        _db.Dictionaries.Remove(dict);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<DictionaryValueDto>> GetValuesAsync(int dictionaryId, bool includeArchived = false, CancellationToken ct = default)
    {
        var tenantId = GetCurrentTenantId();
        
        var query = _db.DictionaryValues
            .Where(v => v.DictionaryId == dictionaryId && v.Dictionary.TenantId == tenantId);

        if (!includeArchived)
            query = query.Where(v => !v.IsArchived);

        return await query
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Id)
            .Select(v => ToValueDto(v))
            .ToListAsync(ct);
    }

    public async Task<DictionaryValueDto?> CreateValueAsync(int dictionaryId, CreateDictionaryValueRequest request, CancellationToken ct = default)
    {
        var tenantId = GetCurrentTenantId();
        
        if (!await _db.Dictionaries.AnyAsync(d => d.Id == dictionaryId && d.TenantId == tenantId, ct))
            return null;

        if (await _db.DictionaryValues.AnyAsync(v => v.DictionaryId == dictionaryId && v.Code == request.Code, ct))
            throw new InvalidOperationException($"Значение с кодом '{request.Code}' уже существует в этом справочнике");

        var value = new DictionaryValue
        {
            DictionaryId = dictionaryId,
            Code = request.Code,
            Name = request.Name,
            IsArchived = false,
            SortOrder = request.SortOrder,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.DictionaryValues.Add(value);
        await _db.SaveChangesAsync(ct);

        return ToValueDto(value);
    }

    public async Task<DictionaryValueDto?> UpdateValueAsync(int dictionaryId, int valueId, UpdateDictionaryValueRequest request, CancellationToken ct = default)
    {
        var tenantId = GetCurrentTenantId();
        
        var value = await _db.DictionaryValues
            .Where(v => v.Id == valueId && v.DictionaryId == dictionaryId && v.Dictionary.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);

        if (value is null) return null;

        value.Name = request.Name;
        value.SortOrder = request.SortOrder;
        await _db.SaveChangesAsync(ct);

        return ToValueDto(value);
    }

    public async Task<bool> DeleteValueAsync(int dictionaryId, int valueId, CancellationToken ct = default)
    {
        var tenantId = GetCurrentTenantId();
        
        var value = await _db.DictionaryValues
            .Where(v => v.Id == valueId && v.DictionaryId == dictionaryId && v.Dictionary.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);

        if (value is null) return false;

        // SPR-04: удалять можно только архивированные (они не используются)
        if (!value.IsArchived)
            throw new InvalidOperationException("Нельзя удалить активное значение. Сначала архивируйте его.");

        // Проверяем, не используется ли значение в других сущностях
        await CheckIfValueIsUsedAsync(value, ct);

        _db.DictionaryValues.Remove(value);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task CheckIfValueIsUsedAsync(DictionaryValue value, CancellationToken ct)
    {
        // Получаем информацию о справочнике
        var dictionary = await _db.Dictionaries.FindAsync(new object[] { value.DictionaryId }, cancellationToken: ct);
        if (dictionary is null) return;

        // Проверяем, используется ли значение в Asset.Status
        if (dictionary.Code == "asset_status")
        {
            var usedInAssetStatus = await _db.Set<Asset>()
                .AnyAsync(a => a.Status == value.Code, ct);
            if (usedInAssetStatus)
                throw new InvalidOperationException($"Значение '{value.Name}' используется в статусах активов и не может быть удалено.");
        }

        // Проверяем, используется ли значение в Asset.Category
        if (dictionary.Code == "asset_category")
        {
            var usedInAssetCategory = await _db.Set<Asset>()
                .AnyAsync(a => a.Category == value.Code, ct);
            if (usedInAssetCategory)
                throw new InvalidOperationException($"Значение '{value.Name}' используется в категориях активов и не может быть удалено.");
        }

        // Проверяем, используется ли значение в Ticket.Priority
        if (dictionary.Code == "ticket_priority")
        {
            var usedInTicketPriority = await _db.Set<Ticket>()
                .AnyAsync(t => t.Priority == value.Code, ct);
            if (usedInTicketPriority)
                throw new InvalidOperationException($"Значение '{value.Name}' используется в приоритетах тикетов и не может быть удалено.");
        }
    }

    public async Task<DictionaryValueDto?> ArchiveValueAsync(int dictionaryId, int valueId, CancellationToken ct = default)
    {
        var tenantId = GetCurrentTenantId();
        
        var value = await _db.DictionaryValues
            .Where(v => v.Id == valueId && v.DictionaryId == dictionaryId && v.Dictionary.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);

        if (value is null) return null;

        value.IsArchived = true;
        await _db.SaveChangesAsync(ct);

        return ToValueDto(value);
    }

    private static DictionaryValueDto ToValueDto(DictionaryValue v) => new()
    {
        Id = v.Id,
        DictionaryId = v.DictionaryId,
        Code = v.Code,
        Name = v.Name,
        IsArchived = v.IsArchived,
        SortOrder = v.SortOrder
    };
}
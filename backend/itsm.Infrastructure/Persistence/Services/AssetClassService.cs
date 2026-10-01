using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Persistence.Services;

public class AssetClassService : IAssetClassService
{
    private readonly ItsmDbContext _db;

    public AssetClassService(ItsmDbContext db)
    {
        _db = db;
    }

    // ==================== AssetClass ====================

    public async Task<List<AssetClassDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.AssetClasses
            .OrderBy(c => c.Id)
            .Select(c => new AssetClassDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                IsActive = c.IsActive,
                AttributesCount = c.Attributes.Count,
                AssetsCount = c.Assets.Count
            })
            .ToListAsync(ct);
    }

    public async Task<AssetClassDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.AssetClasses
            .Include(c => c.Attributes)
            .Include(c => c.Assets)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (entity is null) return null;

        return new AssetClassDetailDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            AttributesCount = entity.Attributes.Count,
            AssetsCount = entity.Assets.Count,
            Attributes = entity.Attributes
                .OrderBy(a => a.SortOrder).ThenBy(a => a.Id)
                .Select(ToAttributeDto)
                .ToList()
        };
    }

    public async Task<AssetClassDto> CreateAsync(CreateAssetClassRequest request, CancellationToken ct = default)
    {
        if (await _db.AssetClasses.AnyAsync(c => c.Code == request.Code, ct))
            throw new InvalidOperationException($"Класс активов с кодом '{request.Code}' уже существует");

        var entity = new AssetClass
        {
            Code = request.Code,
            Name = request.Name,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.AssetClasses.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new AssetClassDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            IsActive = entity.IsActive,
            AttributesCount = 0,
            AssetsCount = 0
        };
    }

    public async Task<AssetClassDto?> UpdateAsync(int id, UpdateAssetClassRequest request, CancellationToken ct = default)
    {
        var entity = await _db.AssetClasses
            .Include(c => c.Attributes)
            .Include(c => c.Assets)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (entity is null) return null;

        entity.Name = request.Name;
        await _db.SaveChangesAsync(ct);

        return new AssetClassDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            IsActive = entity.IsActive,
            AttributesCount = entity.Attributes.Count,
            AssetsCount = entity.Assets.Count
        };
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.AssetClasses
            .Include(c => c.Attributes)
            .Include(c => c.Assets)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (entity is null) return false;

        // По аналогии со справочниками (SPR-04): нельзя удалять используемый класс
        if (entity.Assets.Any())
            throw new InvalidOperationException(
                $"Нельзя удалить класс активов '{entity.Name}': к нему привязано активов — {entity.Assets.Count}. " +
                "Сначала перенесите активы в другой класс или деактивируйте класс.");

        _db.AssetClassAttributes.RemoveRange(entity.Attributes);
        _db.AssetClasses.Remove(entity);

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AssetClassDto?> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.AssetClasses
            .Include(c => c.Attributes)
            .Include(c => c.Assets)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (entity is null) return null;

        entity.IsActive = false;
        await _db.SaveChangesAsync(ct);

        return new AssetClassDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            IsActive = entity.IsActive,
            AttributesCount = entity.Attributes.Count,
            AssetsCount = entity.Assets.Count
        };
    }

    public async Task<AssetClassDto?> ActivateAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.AssetClasses
            .Include(c => c.Attributes)
            .Include(c => c.Assets)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (entity is null) return null;

        entity.IsActive = true;
        await _db.SaveChangesAsync(ct);

        return new AssetClassDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            IsActive = entity.IsActive,
            AttributesCount = entity.Attributes.Count,
            AssetsCount = entity.Assets.Count
        };
    }

    // ==================== AssetClassAttribute ====================

    public async Task<List<AssetClassAttributeDto>> GetAttributesAsync(int classId, CancellationToken ct = default)
    {
        return await _db.AssetClassAttributes
            .Where(a => a.AssetClassId == classId)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Id)
            .Select(a => ToAttributeDto(a))
            .ToListAsync(ct);
    }

    public async Task<AssetClassAttributeDto?> GetAttributeAsync(int classId, int attributeId, CancellationToken ct = default)
    {
        var entity = await _db.AssetClassAttributes
            .FirstOrDefaultAsync(a => a.Id == attributeId && a.AssetClassId == classId, ct);

        return entity is null ? null : ToAttributeDto(entity);
    }

    public async Task<AssetClassAttributeDto?> CreateAttributeAsync(int classId, CreateAssetClassAttributeRequest request, CancellationToken ct = default)
    {
        if (!await _db.AssetClasses.AnyAsync(c => c.Id == classId, ct))
            return null;

        if (await _db.AssetClassAttributes.AnyAsync(a => a.AssetClassId == classId && a.Code == request.Code, ct))
            throw new InvalidOperationException($"Атрибут с кодом '{request.Code}' уже существует в этом классе");

        var entity = new AssetClassAttribute
        {
            AssetClassId = classId,
            Code = request.Code,
            Name = request.Name,
            DataType = request.DataType,
            IsRequired = request.IsRequired,
            DefaultValue = request.DefaultValue,
            Options = request.Options,
            SortOrder = request.SortOrder,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.AssetClassAttributes.Add(entity);
        await _db.SaveChangesAsync(ct);

        return ToAttributeDto(entity);
    }

    public async Task<AssetClassAttributeDto?> UpdateAttributeAsync(int classId, int attributeId, UpdateAssetClassAttributeRequest request, CancellationToken ct = default)
    {
        var entity = await _db.AssetClassAttributes
            .FirstOrDefaultAsync(a => a.Id == attributeId && a.AssetClassId == classId, ct);

        if (entity is null) return null;

        entity.Name = request.Name;
        entity.DataType = request.DataType;
        entity.IsRequired = request.IsRequired;
        entity.DefaultValue = request.DefaultValue;
        entity.Options = request.Options;
        entity.SortOrder = request.SortOrder;

        await _db.SaveChangesAsync(ct);

        return ToAttributeDto(entity);
    }

    public async Task<bool> DeleteAttributeAsync(int classId, int attributeId, CancellationToken ct = default)
    {
        var entity = await _db.AssetClassAttributes
            .FirstOrDefaultAsync(a => a.Id == attributeId && a.AssetClassId == classId, ct);

        if (entity is null) return false;

        _db.AssetClassAttributes.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ==================== Helpers ====================

    private static AssetClassAttributeDto ToAttributeDto(AssetClassAttribute a) => new()
    {
        Id = a.Id,
        AssetClassId = a.AssetClassId,
        Code = a.Code,
        Name = a.Name,
        DataType = a.DataType,
        IsRequired = a.IsRequired,
        DefaultValue = a.DefaultValue,
        Options = a.Options,
        SortOrder = a.SortOrder
    };
}
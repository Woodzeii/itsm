using itsm.Application.Common.Models;

namespace itsm.Application.Common.Interfaces;

public interface IAssetClassService
{
    // AssetClass
    Task<List<AssetClassDto>> GetAllAsync(CancellationToken ct = default);
    Task<AssetClassDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<AssetClassDto> CreateAsync(CreateAssetClassRequest request, CancellationToken ct = default);
    Task<AssetClassDto?> UpdateAsync(int id, UpdateAssetClassRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<AssetClassDto?> DeactivateAsync(int id, CancellationToken ct = default);
    Task<AssetClassDto?> ActivateAsync(int id, CancellationToken ct = default);

    // AssetClassAttribute
    Task<List<AssetClassAttributeDto>> GetAttributesAsync(int classId, CancellationToken ct = default);
    Task<AssetClassAttributeDto?> GetAttributeAsync(int classId, int attributeId, CancellationToken ct = default);
    Task<AssetClassAttributeDto?> CreateAttributeAsync(int classId, CreateAssetClassAttributeRequest request, CancellationToken ct = default);
    Task<AssetClassAttributeDto?> UpdateAttributeAsync(int classId, int attributeId, UpdateAssetClassAttributeRequest request, CancellationToken ct = default);
    Task<bool> DeleteAttributeAsync(int classId, int attributeId, CancellationToken ct = default);
}
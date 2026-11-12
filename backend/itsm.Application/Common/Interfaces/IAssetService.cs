using itsm.Application.Common.Models;

namespace itsm.Application.Common.Interfaces;

public interface IAssetService
{
    Task<IReadOnlyList<AssetDto>> GetAllAsync(int? assetClassId, string? lifecycleStage, string? search, CancellationToken ct = default);
    Task<AssetDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<AssetDto> CreateAsync(CreateAssetRequest request, int actorId, CancellationToken ct = default);
    Task<AssetDto?> UpdateAsync(int id, UpdateAssetRequest request, int actorId, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, int actorId, CancellationToken ct = default);

    Task<AssetMovementDto?> RegisterMovementAsync(int id, RegisterAssetMovementRequest request, int actorId, CancellationToken ct = default);
    Task<IReadOnlyList<AssetMovementDto>?> GetMovementsAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<AssetHistoryItemDto>?> GetHistoryAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<AssetTicketHistoryItem>?> GetTicketsAsync(int id, CancellationToken ct = default);
}

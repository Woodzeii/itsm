using itsm.Application.Common.Models;

namespace itsm.Application.Common.Interfaces;

public interface IAssetService
{
    Task<List<AssetDto>> GetAllAsync(int? classId, string? status, CancellationToken ct = default);
    Task<AssetDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<AssetDto> CreateAsync(CreateAssetRequest request, int performedById, CancellationToken ct = default);
    Task<AssetDto?> UpdateAsync(int id, UpdateAssetRequest request, int performedById, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<AssetMovementDto?> RegisterMovementAsync(int id, RegisterAssetMovementRequest request, int performedById, CancellationToken ct = default);
    Task<List<AssetHistoryItemDto>> GetHistoryAsync(int id, CancellationToken ct = default);
    Task<List<AssetTicketHistoryItemDto>> GetTicketsAsync(int id, CancellationToken ct = default);
}

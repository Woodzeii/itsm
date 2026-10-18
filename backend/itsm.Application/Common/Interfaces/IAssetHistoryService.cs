namespace itsm.Application.Common.Interfaces;

public interface IAssetHistoryService
{
    Task LogAsync(
        int assetId,
        string actionType,
        string description,
        int? userId = null,
        string? details = null,
        int? ticketId = null,
        string? comment = null,
        CancellationToken ct = default);
}

namespace itsm.Application.Common.Interfaces;

using itsm.Application.Common.Models;

public interface IReportService
{
    Task<IReadOnlyList<ReportSummaryResponse>> GetSummariesAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<ReportDetailResponse?> GetDetailsAsync(string reportType, DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct = default);
    Task<byte[]?> ExportCsvAsync(string reportType, DateOnly? from, DateOnly? to, CancellationToken ct = default);
}

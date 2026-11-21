namespace itsm.Application.Common.Models;

public sealed record ReportSummaryResponse(string Id, string Title, string Description, int Count);

public sealed record ReportColumn(string Key, string Label);

public sealed record ReportDetailRow(Dictionary<string, string> Values);

public sealed record ReportDetailResponse(
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<ReportDetailRow> Items,
    int Total,
    int Skip,
    int Take);

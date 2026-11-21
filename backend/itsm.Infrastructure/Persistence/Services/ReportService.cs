using System.Globalization;
using System.Text;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Persistence.Services;

public sealed class ReportService(ItsmDbContext db) : IReportService
{
    private const int PageSizeLimit = 100;
    private static readonly string[] SupportedTypes =
    [
        "tickets-status", "tickets-type", "engineer-load", "returns",
        "repairs", "assets-stage", "asset-movements"
    ];

    public async Task<IReadOnlyList<ReportSummaryResponse>> GetSummariesAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        ValidateRange(from, to);
        var tickets = FilterTickets(db.Tickets.AsNoTracking(), from, to);
        var assets = FilterAssets(db.Assets.AsNoTracking(), from, to);
        var movements = FilterMovements(db.AssetMovements.AsNoTracking(), from, to);
        var ticketCount = await tickets.CountAsync(ct);
        var assignedCount = await tickets.CountAsync(ticket => ticket.AssigneeId != null, ct);
        var repairCount = await tickets.CountAsync(ticket => ticket.Type.Code == "repair", ct);
        var returnCount = await movements.CountAsync(item => item.MovementType == AssetMovementTypes.Return, ct);
        var assetCount = await assets.CountAsync(ct);
        var movementCount = await movements.CountAsync(ct);

        return
        [
            new("tickets-status", "Заявки по статусам", "Количество заявок в статусах за период", ticketCount),
            new("tickets-type", "Заявки по типам", "Распределение заявок по типам", ticketCount),
            new("engineer-load", "Нагрузка исполнителей", "Назначенные заявки исполнителям", assignedCount),
            new("returns", "Возвраты активов", "Возвраты активов на склад за период", returnCount),
            new("repairs", "Ремонты", "Заявки на ремонт за период", repairCount),
            new("assets-stage", "Активы по этапам", "Активы по этапам жизненного цикла", assetCount),
            new("asset-movements", "Движения активов", "Операции перемещения активов", movementCount)
        ];
    }

    public async Task<ReportDetailResponse?> GetDetailsAsync(
        string reportType,
        DateOnly? from,
        DateOnly? to,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        ValidateRange(from, to);
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take is < 1 or > PageSizeLimit) throw new ArgumentOutOfRangeException(nameof(take));

        return reportType switch
        {
            "tickets-status" => await TicketStatuses(from, to, skip, take, ct),
            "tickets-type" => await TicketTypes(from, to, skip, take, ct),
            "engineer-load" => await EngineerLoad(from, to, skip, take, ct),
            "returns" => await Returns(from, to, skip, take, ct),
            "repairs" => await Repairs(from, to, skip, take, ct),
            "assets-stage" => await AssetStages(from, to, skip, take, ct),
            "asset-movements" => await Movements(from, to, skip, take, ct),
            _ => null
        };
    }

    public async Task<byte[]?> ExportCsvAsync(
        string reportType,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        ValidateRange(from, to);
        if (!SupportedTypes.Contains(reportType, StringComparer.Ordinal))
            return null;

        var firstPage = await GetDetailsAsync(reportType, from, to, 0, PageSizeLimit, ct);
        if (firstPage is null) return null;
        var rows = firstPage.Items.ToList();
        while (rows.Count < firstPage.Total)
        {
            var page = await GetDetailsAsync(reportType, from, to, rows.Count, PageSizeLimit, ct);
            if (page is null || page.Items.Count == 0) break;
            rows.AddRange(page.Items);
        }

        var output = new StringBuilder();
        output.Append('\uFEFF');
        AppendCsvRow(output, firstPage.Columns.Select(column => column.Label));
        foreach (var row in rows)
            AppendCsvRow(output, firstPage.Columns.Select(column =>
                row.Values.TryGetValue(column.Key, out var value) ? value : string.Empty));
        return Encoding.UTF8.GetBytes(output.ToString());
    }

    private async Task<ReportDetailResponse> TicketStatuses(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var query = FilterTickets(db.Tickets.AsNoTracking(), from, to)
            .GroupBy(ticket => ticket.Status.Name)
            .Select(group => new { Name = group.Key, Count = group.Count() })
            .OrderBy(item => item.Name);
        var total = await query.CountAsync(ct);
        var rows = await query.Skip(skip).Take(take).ToArrayAsync(ct);
        return Result(
            [new("status", "Статус"), new("count", "Количество")],
            rows.Select(item => Row(("status", item.Name), ("count", Number(item.Count)))),
            total, skip, take);
    }

    private async Task<ReportDetailResponse> TicketTypes(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var query = FilterTickets(db.Tickets.AsNoTracking(), from, to)
            .GroupBy(ticket => ticket.Type.Name)
            .Select(group => new { Name = group.Key, Count = group.Count() })
            .OrderBy(item => item.Name);
        var total = await query.CountAsync(ct);
        var rows = await query.Skip(skip).Take(take).ToArrayAsync(ct);
        return Result(
            [new("type", "Тип заявки"), new("count", "Количество")],
            rows.Select(item => Row(("type", item.Name), ("count", Number(item.Count)))),
            total, skip, take);
    }

    private async Task<ReportDetailResponse> EngineerLoad(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var query = FilterTickets(db.Tickets.AsNoTracking(), from, to)
            .Where(ticket => ticket.AssigneeId != null)
            .GroupBy(ticket => ticket.Assignee!.FullName)
            .Select(group => new { Name = group.Key, Count = group.Count() })
            .OrderBy(item => item.Name);
        var total = await query.CountAsync(ct);
        var rows = await query.Skip(skip).Take(take).ToArrayAsync(ct);
        return Result(
            [new("assignee", "Исполнитель"), new("count", "Назначено заявок")],
            rows.Select(item => Row(("assignee", item.Name), ("count", Number(item.Count)))),
            total, skip, take);
    }

    private async Task<ReportDetailResponse> Returns(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var query = FilterMovements(db.AssetMovements.AsNoTracking(), from, to)
            .Where(item => item.MovementType == AssetMovementTypes.Return)
            .OrderByDescending(item => item.PerformedAt);
        var total = await query.CountAsync(ct);
        var rows = await query.Skip(skip).Take(take)
            .Select(item => new
            {
                Asset = item.Asset.InventoryNumber,
                item.Asset.Name,
                Actor = item.PerformedBy.FullName,
                item.PerformedAt
            }).ToArrayAsync(ct);
        return Result(
            [new("asset", "Инв. номер"), new("name", "Актив"), new("actor", "Выполнил"), new("date", "Дата")],
            rows.Select(item => Row(
                ("asset", item.Asset), ("name", item.Name), ("actor", item.Actor), ("date", FormatDate(item.PerformedAt)))),
            total, skip, take);
    }

    private async Task<ReportDetailResponse> Repairs(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var query = FilterTickets(db.Tickets.AsNoTracking(), from, to)
            .Where(ticket => ticket.Type.Code == "repair")
            .OrderByDescending(ticket => ticket.CreatedAt);
        var total = await query.CountAsync(ct);
        var rows = await query.Skip(skip).Take(take)
            .Select(ticket => new
            {
                ticket.TicketNumber,
                ticket.Title,
                Status = ticket.Status.Name,
                ticket.CreatedAt
            }).ToArrayAsync(ct);
        return Result(
            [new("number", "Номер"), new("title", "Заявка"), new("status", "Статус"), new("created", "Создана")],
            rows.Select(item => Row(
                ("number", item.TicketNumber), ("title", item.Title), ("status", item.Status), ("created", FormatDate(item.CreatedAt)))),
            total, skip, take);
    }

    private async Task<ReportDetailResponse> AssetStages(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var query = FilterAssets(db.Assets.AsNoTracking(), from, to)
            .GroupBy(asset => asset.LifecycleStage)
            .Select(group => new { Stage = group.Key, Count = group.Count() })
            .OrderBy(item => item.Stage);
        var total = await query.CountAsync(ct);
        var rows = await query.Skip(skip).Take(take).ToArrayAsync(ct);
        return Result(
            [new("stage", "Этап"), new("count", "Количество активов")],
            rows.Select(item => Row(
                ("stage", AssetLifecycleStages.GetRussianName(item.Stage)), ("count", Number(item.Count)))),
            total, skip, take);
    }

    private async Task<ReportDetailResponse> Movements(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var query = FilterMovements(db.AssetMovements.AsNoTracking(), from, to)
            .OrderByDescending(item => item.PerformedAt);
        var total = await query.CountAsync(ct);
        var rows = await query.Skip(skip).Take(take)
            .Select(item => new
            {
                Asset = item.Asset.InventoryNumber,
                item.Asset.Name,
                item.MovementType,
                Actor = item.PerformedBy.FullName,
                item.PerformedAt
            }).ToArrayAsync(ct);
        return Result(
            [new("asset", "Инв. номер"), new("name", "Актив"), new("type", "Движение"), new("actor", "Выполнил"), new("date", "Дата")],
            rows.Select(item => Row(
                ("asset", item.Asset),
                ("name", item.Name),
                ("type", AssetMovementTypes.GetRussianName(item.MovementType)),
                ("actor", item.Actor),
                ("date", FormatDate(item.PerformedAt)))),
            total, skip, take);
    }

    private IQueryable<Ticket> FilterTickets(IQueryable<Ticket> query, DateOnly? from, DateOnly? to)
    {
        if (from is DateOnly start) query = query.Where(item => item.CreatedAt >= StartOfDay(start));
        if (to is DateOnly end) query = query.Where(item => item.CreatedAt < StartOfDay(end.AddDays(1)));
        return query;
    }

    private IQueryable<Asset> FilterAssets(IQueryable<Asset> query, DateOnly? from, DateOnly? to)
    {
        if (from is DateOnly start) query = query.Where(item => item.CreatedAt >= StartOfDay(start));
        if (to is DateOnly end) query = query.Where(item => item.CreatedAt < StartOfDay(end.AddDays(1)));
        return query;
    }

    private IQueryable<AssetMovement> FilterMovements(IQueryable<AssetMovement> query, DateOnly? from, DateOnly? to)
    {
        query = query.Where(item => item.Asset.TenantId == db.GetCurrentTenantId());
        if (from is DateOnly start) query = query.Where(item => item.PerformedAt >= StartOfDay(start));
        if (to is DateOnly end) query = query.Where(item => item.PerformedAt < StartOfDay(end.AddDays(1)));
        return query;
    }

    private static DateTimeOffset StartOfDay(DateOnly day) =>
        new(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

    private static string FormatDate(DateTimeOffset value) =>
        value.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static ReportDetailResponse Result(
        IReadOnlyList<ReportColumn> columns,
        IEnumerable<ReportDetailRow> items,
        int total,
        int skip,
        int take) => new(columns, items.ToArray(), total, skip, take);

    private static ReportDetailRow Row(params (string Key, string Value)[] values) =>
        new(values.ToDictionary(item => item.Key, item => item.Value));

    private static void AppendCsvRow(StringBuilder output, IEnumerable<string> values)
    {
        output.AppendJoin(';', values.Select(EscapeCsv));
        output.Append("\r\n");
    }

    private static string EscapeCsv(string value) =>
        "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";

    private static void ValidateRange(DateOnly? from, DateOnly? to)
    {
        if (from is DateOnly start && to is DateOnly end && start > end)
            throw new ArgumentException("Дата начала не может быть позже даты окончания.");
    }
}

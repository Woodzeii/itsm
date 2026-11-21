using System.Text;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Persistence.Services;

public sealed class ReportService(ItsmDbContext db) : IReportService
{
    private static readonly HashSet<string> ReportTypes = new(StringComparer.Ordinal)
    {
        "tickets-status", "tickets-type", "engineer-load", "returns",
        "repairs", "assets-stage", "asset-movements"
    };

    public async Task<IReadOnlyList<ReportSummaryResponse>> GetSummariesAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        ValidateRange(from, to);
        var tickets = db.Tickets.AsNoTracking();
        var assets = db.Assets.AsNoTracking();
        var movements = db.AssetMovements.AsNoTracking();
        tickets = ApplyTicketRange(tickets, from, to);
        assets = ApplyAssetRange(assets, from, to);
        movements = ApplyMovementRange(movements, from, to);

        var ticketCount = await tickets.CountAsync(ct);
        var repairCount = await tickets.CountAsync(ticket => ticket.Type.Code == "repair", ct);
        var returnCount = await movements.CountAsync(movement => movement.MovementType == AssetMovementTypes.Return, ct);
        var assetCount = await assets.CountAsync(ct);
        var movementCount = await movements.CountAsync(ct);
        var assignedCount = await tickets.CountAsync(ticket => ticket.AssigneeId != null, ct);
        var statusCount = await db.TicketStatuses.AsNoTracking()
            .Where(status => status.TenantId == db.GetCurrentTenantId())
            .CountAsync(ct);
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
        if (!ReportTypes.Contains(reportType)) return null;
        ValidateRange(from, to);
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(take));

        var result = reportType switch
        {
            "tickets-status" => await GetTicketStatusDetails(from, to, skip, take, ct),
            "tickets-type" => await GetTicketTypeDetails(from, to, skip, take, ct),
            "engineer-load" => await GetEngineerLoadDetails(from, to, skip, take, ct),
            "returns" => await GetReturnDetails(from, to, skip, take, ct),
            "repairs" => await GetRepairDetails(from, to, skip, take, ct),
            "assets-stage" => await GetAssetStageDetails(from, to, skip, take, ct),
            "asset-movements" => await GetMovementDetails(from, to, skip, take, ct),
            _ => null
        };
        return result;
    }

    public async Task<byte[]?> ExportCsvAsync(
        string reportType,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        var report = await GetDetailsAsync(reportType, from, to, 0, 1000, ct);
        if (report is null) return null;

        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        sb.AppendJoin(';', report.Columns.Select(column => EscapeCsv(column.Label)));
        sb.AppendLine();
        foreach (var item in report.Items)
        {
            sb.AppendJoin(';', report.Columns.Select(column =>
                EscapeCsv(item.Values.TryGetValue(column.Key, out var value) ? value : string.Empty)));
            sb.AppendLine();
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private async Task<ReportDetailResponse> GetTicketStatusDetails(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var query = ApplyTicketRange(db.Tickets.AsNoTracking(), from, to)
            .Include(ticket => ticket.Status);
        var total = await query.CountAsync(ct);
        var data = await query.OrderByDescending(ticket => ticket.CreatedAt)
            .Skip(skip).Take(take)
            .Select(ticket => new
            {
                ticket.TicketNumber,
                ticket.Title,
                Status = ticket.Status.Name,
                Created = ticket.CreatedAt
            }).ToArrayAsync(ct);
        var rows = data.Select(row => new ReportDetailRow(new Dictionary<string, string>
        {
            ["number"] = row.TicketNumber,
            ["title"] = row.Title,
            ["status"] = row.Status,
            ["created"] = row.Created.ToString("dd.MM.yyyy HH:mm")
        })).ToArray();
        return BuildDetails(["number", "title", "status", "created"],
            [new("number", "Номер"), new("title", "Тема"), new("status", "Статус"), new("created", "Создана")],
            rows, total, skip, take);
    }

    private async Task<ReportDetailResponse> GetTicketTypeDetails(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var query = ApplyTicketRange(db.Tickets.AsNoTracking(), from, to).Include(ticket => ticket.Type);
        var total = await query.CountAsync(ct);
        var data = await query.OrderByDescending(ticket => ticket.CreatedAt).Skip(skip).Take(take)
            .Select(ticket => new
            {
                ticket.TicketNumber,
                ticket.Title,
                Type = ticket.Type.Name,
                ticket.CreatedAt
            }).ToArrayAsync(ct);
        var rows = data.Select(row => new ReportDetailRow(new Dictionary<string, string>
        {
            ["number"] = row.TicketNumber,
            ["title"] = row.Title,
            ["type"] = row.Type,
            ["created"] = row.CreatedAt.ToString("dd.MM.yyyy HH:mm")
        })).ToArray();
        return BuildDetails(["number", "title", "type", "created"],
            [new("number", "Номер"), new("title", "Тема"), new("type", "Тип"), new("created", "Создана")],
            rows, total, skip, take);
    }

    private async Task<ReportDetailResponse> GetEngineerLoadDetails(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var query = ApplyTicketRange(db.Tickets.AsNoTracking(), from, to)
            .Where(ticket => ticket.AssigneeId != null);
        var total = await query.CountAsync(ct);
        var data = await query.Include(ticket => ticket.Assignee).OrderBy(ticket => ticket.Assignee!.FullName)
            .ThenByDescending(ticket => ticket.CreatedAt).Skip(skip).Take(take)
            .Select(ticket => new
            {
                ticket.TicketNumber,
                ticket.Title,
                Assignee = ticket.Assignee == null ? "Не назначен" : ticket.Assignee.FullName,
                Status = ticket.Status.Name
            }).ToArrayAsync(ct);
        var rows = data.Select(row => new ReportDetailRow(new Dictionary<string, string>
        {
            ["number"] = row.TicketNumber,
            ["title"] = row.Title,
            ["assignee"] = row.Assignee,
            ["status"] = row.Status
        })).ToArray();
        return BuildDetails(["number", "title", "assignee", "status"],
            [new("number", "Номер"), new("title", "Тема"), new("assignee", "Исполнитель"), new("status", "Статус")],
            rows, total, skip, take);
    }

    private async Task<ReportDetailResponse> GetReturnDetails(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var query = ApplyMovementRange(db.AssetMovements.AsNoTracking(), from, to)
            .Include(movement => movement.Asset)
            .Include(movement => movement.PerformedBy)
            .Where(movement => movement.MovementType == AssetMovementTypes.Return);
        var total = await query.CountAsync(ct);
        var data = await query.OrderByDescending(movement => movement.PerformedAt).Skip(skip).Take(take)
            .Select(movement => new
            {
                AssetNumber = movement.Asset.InventoryNumber,
                movement.Asset.Name,
                Actor = movement.PerformedBy.FullName,
                movement.PerformedAt
            }).ToArrayAsync(ct);
        var rows = data.Select(row => new ReportDetailRow(new Dictionary<string, string>
        {
            ["asset"] = row.AssetNumber,
            ["name"] = row.Name,
            ["actor"] = row.Actor,
            ["date"] = row.PerformedAt.ToString("dd.MM.yyyy HH:mm")
        })).ToArray();
        return BuildDetails(["asset", "name", "actor", "date"],
            [new("asset", "Инв. номер"), new("name", "Актив"), new("actor", "Исполнитель"), new("date", "Дата")],
            rows, total, skip, take);
    }

    private async Task<ReportDetailResponse> GetRepairDetails(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var query = ApplyTicketRange(db.Tickets.AsNoTracking(), from, to)
            .Include(ticket => ticket.Status)
            .Where(ticket => ticket.Type.Code == "repair");
        var total = await query.CountAsync(ct);
        var data = await query.OrderByDescending(ticket => ticket.CreatedAt).Skip(skip).Take(take)
            .Select(ticket => new
            {
                ticket.TicketNumber,
                ticket.Title,
                Status = ticket.Status.Name,
                ticket.CreatedAt
            }).ToArrayAsync(ct);
        var rows = data.Select(row => new ReportDetailRow(new Dictionary<string, string>
        {
            ["number"] = row.TicketNumber,
            ["title"] = row.Title,
            ["status"] = row.Status,
            ["created"] = row.CreatedAt.ToString("dd.MM.yyyy HH:mm")
        })).ToArray();
        return BuildDetails(["number", "title", "status", "created"],
            [new("number", "Номер"), new("title", "Тема"), new("status", "Статус"), new("created", "Создана")],
            rows, total, skip, take);
    }

    private async Task<ReportDetailResponse> GetAssetStageDetails(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var query = ApplyAssetRange(db.Assets.AsNoTracking(), from, to);
        var total = await query.CountAsync(ct);
        var data = await query.Include(asset => asset.AssetClass).OrderBy(asset => asset.InventoryNumber).Skip(skip).Take(take)
            .Select(asset => new
            {
                asset.InventoryNumber,
                asset.Name,
                asset.LifecycleStage,
                Class = asset.AssetClass == null ? "" : asset.AssetClass.Name
            }).ToArrayAsync(ct);
        var rows = data.Select(asset => new ReportDetailRow(new Dictionary<string, string>
        {
            ["number"] = asset.InventoryNumber,
            ["name"] = asset.Name,
            ["stage"] = AssetLifecycleStages.GetRussianName(asset.LifecycleStage),
            ["class"] = asset.Class
        })).ToArray();
        return BuildDetails(["number", "name", "stage", "class"],
            [new("number", "Инв. номер"), new("name", "Название"), new("stage", "Этап"), new("class", "Класс")],
            rows, total, skip, take);
    }

    private async Task<ReportDetailResponse> GetMovementDetails(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var query = ApplyMovementRange(db.AssetMovements.AsNoTracking(), from, to)
            .Include(movement => movement.Asset)
            .Include(movement => movement.PerformedBy);
        var total = await query.CountAsync(ct);
        var data = await query.OrderByDescending(movement => movement.PerformedAt).Skip(skip).Take(take)
            .Select(movement => new
            {
                AssetNumber = movement.Asset.InventoryNumber,
                movement.Asset.Name,
                movement.MovementType,
                Actor = movement.PerformedBy.FullName,
                movement.PerformedAt
            }).ToArrayAsync(ct);
        var rows = data.Select(movement => new ReportDetailRow(new Dictionary<string, string>
        {
            ["asset"] = movement.AssetNumber,
            ["name"] = movement.Name,
            ["type"] = AssetMovementTypes.GetRussianName(movement.MovementType),
            ["actor"] = movement.Actor,
            ["date"] = movement.PerformedAt.ToString("dd.MM.yyyy HH:mm")
        })).ToArray();
        return BuildDetails(["asset", "name", "type", "actor", "date"],
            [new("asset", "Инв. номер"), new("name", "Актив"), new("type", "Тип"), new("actor", "Исполнитель"), new("date", "Дата")],
            rows, total, skip, take);
    }

    private static ReportDetailResponse BuildDetails(
        IReadOnlyList<string> _keys,
        IReadOnlyList<ReportColumn> columns,
        IReadOnlyList<ReportDetailRow> rows,
        int total,
        int skip,
        int take) => new(columns, rows, total, skip, take);

    private IQueryable<Ticket> ApplyTicketRange(IQueryable<Ticket> query, DateOnly? from, DateOnly? to)
    {
        if (from is DateOnly fromDate)
            query = query.Where(ticket => ticket.CreatedAt >= fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (to is DateOnly toDate)
            query = query.Where(ticket => ticket.CreatedAt < toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        return query;
    }

    private IQueryable<Domain.Entities.Asset> ApplyAssetRange(IQueryable<Domain.Entities.Asset> query, DateOnly? from, DateOnly? to)
    {
        if (from is DateOnly fromDate)
            query = query.Where(asset => asset.CreatedAt >= fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (to is DateOnly toDate)
            query = query.Where(asset => asset.CreatedAt < toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        return query;
    }

    private IQueryable<Domain.Entities.AssetMovement> ApplyMovementRange(IQueryable<Domain.Entities.AssetMovement> query, DateOnly? from, DateOnly? to)
    {
        if (from is DateOnly fromDate)
            query = query.Where(movement => movement.PerformedAt >= fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (to is DateOnly toDate)
            query = query.Where(movement => movement.PerformedAt < toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        return query;
    }

    private static void ValidateRange(DateOnly? from, DateOnly? to)
    {
        if (from is DateOnly fromDate && to is DateOnly toDate && fromDate > toDate)
            throw new ArgumentException("Дата начала не может быть позже даты окончания.");
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(';') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}

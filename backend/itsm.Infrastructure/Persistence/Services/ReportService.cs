using System.Globalization;
using System.Text;
using System.Text.Json;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Persistence.Services;

public sealed class ReportService(ItsmDbContext db) : IReportService
{
    private const int PageSizeLimit = 100;
    private const string ClosedStatusCode = "closed";
    private static readonly string[] SupportedTypes =
    [
        "tickets-status", "tickets-type", "sla-compliance", "engineer-load",
        "engineer-performance", "returns", "repairs", "assets-stage", "asset-movements"
    ];

    public async Task<IReadOnlyList<ReportSummaryResponse>> GetSummariesAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        ValidateRange(from, to);
        var currentTickets = db.Tickets.AsNoTracking();
        var activeTickets = currentTickets.Where(ticket => ticket.Status.Code != ClosedStatusCode);
        var activeTicketCount = await activeTickets.CountAsync(ct);
        var ticketTypeCount = await FilterTickets(currentTickets, from, to).CountAsync(ct);
        var slaCount = (await GetSlaRows(from, to, ct)).Count;
        var closedTickets = await GetClosedTickets(from, to, ct);
        var returnCount = (await GetReturnEvents(from, to, ct)).Count;
        var repairsCount = (await GetRepairRecords(from, to, ct)).Count;
        var assetCount = await db.Assets.AsNoTracking().CountAsync(ct);
        var movementCount = await FilterMovements(db.AssetMovements.AsNoTracking(), from, to).CountAsync(ct);

        return
        [
            new("tickets-status", "Заявки по статусам", "Текущий состав незакрытой очереди", activeTicketCount),
            new("tickets-type", "Заявки по типам и критичности", "Распределение созданных заявок за период", ticketTypeCount),
            new("sla-compliance", "Соблюдение SLA", "Нарушения сроков реакции и решения за период", slaCount),
            new("engineer-load", "Нагрузка исполнителей", "Текущие активные заявки, включая неназначенные", activeTicketCount),
            new("engineer-performance", "Результативность исполнителей", "Закрытые заявки за период", closedTickets.Count),
            new("returns", "Возвраты с проверки", "Возвраты из проверки на доработку за период", returnCount),
            new("repairs", "Ремонты", "Закрытые заявки на ремонт за период", repairsCount),
            new("assets-stage", "Активы по этапам жизненного цикла", "Текущее состояние активов по классам", assetCount),
            new("asset-movements", "Движения активов", "Движения активов за период", movementCount)
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
            "tickets-status" => await TicketStatuses(skip, take, ct),
            "tickets-type" => await TicketTypes(from, to, skip, take, ct),
            "sla-compliance" => await SlaCompliance(from, to, skip, take, ct),
            "engineer-load" => await EngineerLoad(skip, take, ct),
            "engineer-performance" => await EngineerPerformance(from, to, skip, take, ct),
            "returns" => await Returns(from, to, skip, take, ct),
            "repairs" => await Repairs(from, to, skip, take, ct),
            "assets-stage" => await AssetStages(skip, take, ct),
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

    private async Task<ReportDetailResponse> TicketStatuses(int skip, int take, CancellationToken ct)
    {
        var statuses = await db.TicketStatuses.AsNoTracking()
            .Where(status => status.Code != ClosedStatusCode)
            .OrderBy(status => status.SortOrder)
            .ThenBy(status => status.Name)
            .Select(status => new { status.Id, status.Name })
            .ToArrayAsync(ct);
        var counts = await db.Tickets.AsNoTracking()
            .Where(ticket => ticket.Status.Code != ClosedStatusCode)
            .GroupBy(ticket => ticket.StatusId)
            .Select(group => new { StatusId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.StatusId, item => item.Count, ct);

        return Result(
            [new("status", "Статус"), new("count", "Количество заявок")],
            statuses.Skip(skip).Take(take).Select(status =>
                Row(("status", status.Name), ("count", Number(counts.GetValueOrDefault(status.Id))))),
            statuses.Length, skip, take);
    }

    private async Task<ReportDetailResponse> TicketTypes(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var tickets = await FilterTickets(db.Tickets.AsNoTracking(), from, to)
            .Select(ticket => new
            {
                ticket.TypeId,
                Type = ticket.Type.Name,
                ticket.CriticalityLevelId,
                Criticality = ticket.CriticalityLevel == null ? "Не указана" : ticket.CriticalityLevel.Name
            })
            .ToArrayAsync(ct);
        var groups = tickets
            .GroupBy(ticket => new { ticket.TypeId, ticket.CriticalityLevelId })
            .Select(group => new
            {
                Type = group.First().Type,
                Criticality = group.First().Criticality,
                Count = group.Count()
            })
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Criticality)
            .ToArray();

        return Result(
            [new("type", "Тип заявки"), new("criticality", "Критичность"), new("count", "Количество")],
            groups.Skip(skip).Take(take).Select(item => Row(
                ("type", item.Type), ("criticality", item.Criticality), ("count", Number(item.Count)))),
            groups.Length, skip, take);
    }

    private async Task<ReportDetailResponse> SlaCompliance(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var slaRows = await GetSlaRows(from, to, ct);
        var groups = slaRows
            .GroupBy(row => new { row.TypeId, row.CriticalityId })
            .Select(group => new
            {
                Type = group.First().Type,
                Criticality = group.First().Criticality,
                Total = group.Count(),
                ReactionBreaches = group.Count(row => row.ReactionBreached),
                ResolutionBreaches = group.Count(row => row.ResolutionBreached)
            })
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Criticality)
            .ToArray();

        return Result(
            [
                new("type", "Тип заявки"),
                new("criticality", "Критичность"),
                new("total", "Всего заявок с SLA"),
                new("reactionCount", "Нарушено реакции"),
                new("reactionPercent", "Нарушено реакции, %"),
                new("resolutionCount", "Нарушено решения"),
                new("resolutionPercent", "Нарушено решения, %")
            ],
            groups.Skip(skip).Take(take).Select(item => Row(
                ("type", item.Type),
                ("criticality", item.Criticality),
                ("total", Number(item.Total)),
                ("reactionCount", Number(item.ReactionBreaches)),
                ("reactionPercent", Percent(item.ReactionBreaches, item.Total)),
                ("resolutionCount", Number(item.ResolutionBreaches)),
                ("resolutionPercent", Percent(item.ResolutionBreaches, item.Total)))),
            groups.Length, skip, take);
    }

    private async Task<List<SlaTicketRow>> GetSlaRows(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var tickets = await FilterTickets(db.Tickets.AsNoTracking(), from, to)
            .Include(ticket => ticket.Type)
            .Include(ticket => ticket.CriticalityLevel)
            .Include(ticket => ticket.SlaPolicy)
            .ToArrayAsync(ct);
        if (tickets.Length == 0) return [];

        var configs = await db.SlaValues.AsNoTracking()
            .Where(value => !value.IsDisabled
                && value.TicketType.TenantId == db.GetCurrentTenantId())
            .Select(value => new
            {
                value.TicketTypeId,
                value.CriticalityLevelId,
                value.ReactionTimeMinutes,
                value.ResolutionTimeMinutes
            })
            .ToDictionaryAsync(value => (value.TicketTypeId, value.CriticalityLevelId), ct);
        var ticketIds = tickets.Select(ticket => ticket.Id).ToArray();
        var messages = await db.TicketMessages.AsNoTracking()
            .Where(message => ticketIds.Contains(message.TicketId) && message.IsInternal != true)
            .Select(message => new { message.TicketId, message.AuthorId, message.CreatedAt })
            .ToArrayAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var result = new List<SlaTicketRow>();
        foreach (var ticket in tickets)
        {
            configs.TryGetValue((ticket.TypeId, ticket.CriticalityLevelId ?? 0), out var config);
            var reactionTarget = ticket.SlaPolicy?.ReactionTimeMinutes ?? config?.ReactionTimeMinutes;
            var resolutionTarget = ticket.SlaPolicy?.ResolutionTimeMinutes ?? config?.ResolutionTimeMinutes;
            var hasActiveSla = ticket.SlaPolicyId is not null || config is not null;
            if (!hasActiveSla
                || reactionTarget is null && resolutionTarget is null
                    && ticket.SlaReactionDeadline is null && ticket.SlaResolutionDeadline is null)
                continue;

            var requesterId = ticket.RequesterId ?? ticket.CreatorId;
            var reactionAt = ticket.ActualReactionTime ?? messages
                .Where(message => message.TicketId == ticket.Id && message.AuthorId != requesterId)
                .Select(message => (DateTimeOffset?)message.CreatedAt)
                .Min();
            var reactionDeadline = ticket.SlaReactionDeadline
                ?? (reactionTarget is int reactionMinutes
                    ? ticket.CreatedAt.AddMinutes(reactionMinutes)
                    : null);
            var resolutionDeadline = ticket.SlaResolutionDeadline
                ?? (resolutionTarget is int resolutionMinutes
                    ? ticket.CreatedAt.AddMinutes(resolutionMinutes + ticket.SlaAccumulatedPauseMinutes)
                    : null);

            result.Add(new SlaTicketRow(
                ticket.TypeId,
                ticket.CriticalityLevelId,
                ticket.Type.Name,
                ticket.CriticalityLevel?.Name ?? "Не указана",
                reactionDeadline is DateTimeOffset reactionLimit
                    && (reactionAt is DateTimeOffset actualReaction
                        ? actualReaction > reactionLimit
                        : !ticket.SlaTimerPaused && now > reactionLimit),
                resolutionDeadline is DateTimeOffset resolutionLimit
                    && (ticket.ActualResolutionTime is DateTimeOffset actualResolution
                        ? actualResolution > resolutionLimit
                        : !ticket.SlaTimerPaused && now > resolutionLimit)));
        }

        return result;
    }

    private async Task<ReportDetailResponse> EngineerLoad(int skip, int take, CancellationToken ct)
    {
        var assignees = await db.Tickets.AsNoTracking()
            .Where(ticket => ticket.Status.Code != ClosedStatusCode)
            .Select(ticket => ticket.AssigneeId)
            .ToArrayAsync(ct);
        var groups = assignees.GroupBy(id => id)
            .Select(group => new { AssigneeId = group.Key, Count = group.Count() })
            .ToList();
        if (groups.All(group => group.AssigneeId is not null))
            groups.Add(new { AssigneeId = (int?)null, Count = 0 });
        var names = await GetUserNames(groups.Where(group => group.AssigneeId.HasValue)
            .Select(group => group.AssigneeId!.Value), ct);
        var rows = groups.Select(group => new
            {
                Assignee = group.AssigneeId is int id
                    ? names.GetValueOrDefault(id, "Неизвестный исполнитель")
                    : "Не назначено",
                group.Count
            })
            .OrderBy(item => item.Assignee == "Не назначено" ? 1 : 0)
            .ThenBy(item => item.Assignee)
            .ToArray();

        return Result(
            [new("assignee", "Исполнитель"), new("count", "Открытых заявок")],
            rows.Skip(skip).Take(take).Select(item => Row(
                ("assignee", item.Assignee), ("count", Number(item.Count)))),
            rows.Length, skip, take);
    }

    private async Task<ReportDetailResponse> EngineerPerformance(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var closed = await GetClosedTickets(from, to, ct);
        var groups = closed
            .GroupBy(ticket => ticket.AssigneeId)
            .Select(group => new
            {
                AssigneeId = group.Key,
                ClosedCount = group.Count(),
                ReactionTime = Average(group.Select(ticket => ticket.ReactionAt is DateTimeOffset responseAt
                    ? (TimeSpan?)NonNegative(responseAt - ticket.CreatedAt)
                    : null)),
                ResolutionTime = Average(group.Select(ticket => (TimeSpan?)NonNegative(
                    (ticket.ActualResolutionAt ?? ticket.ClosedAt) - ticket.CreatedAt
                    - TimeSpan.FromMinutes(ticket.SlaAccumulatedPauseMinutes))))
            })
            .ToArray();
        var names = await GetUserNames(groups.Where(group => group.AssigneeId.HasValue)
            .Select(group => group.AssigneeId!.Value), ct);
        var rows = groups.Select(group => new
            {
                Assignee = group.AssigneeId is int id
                    ? names.GetValueOrDefault(id, "Неизвестный исполнитель")
                    : "Не назначено",
                group.ClosedCount,
                group.ReactionTime,
                group.ResolutionTime
            })
            .OrderBy(item => item.Assignee == "Не назначено" ? 1 : 0)
            .ThenBy(item => item.Assignee)
            .ToArray();

        return Result(
            [
                new("assignee", "Исполнитель"),
                new("closed", "Закрыто заявок"),
                new("reaction", "Среднее время реакции"),
                new("resolution", "Среднее время решения")
            ],
            rows.Skip(skip).Take(take).Select(item => Row(
                ("assignee", item.Assignee),
                ("closed", Number(item.ClosedCount)),
                ("reaction", FormatDuration(item.ReactionTime)),
                ("resolution", FormatDuration(item.ResolutionTime)))),
            rows.Length, skip, take);
    }

    private async Task<List<ClosedTicketRow>> GetClosedTickets(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var closedStatusId = await db.TicketStatuses.AsNoTracking()
            .Where(status => status.Code == ClosedStatusCode)
            .Select(status => (int?)status.Id)
            .FirstOrDefaultAsync(ct);
        if (closedStatusId is null) return [];

        var tickets = await db.Tickets.AsNoTracking()
            .Where(ticket => ticket.StatusId == closedStatusId.Value)
            .Select(ticket => new
            {
                ticket.Id,
                ticket.TicketNumber,
                ticket.Title,
                Type = ticket.Type.Name,
                Criticality = ticket.CriticalityLevel == null ? "Не указана" : ticket.CriticalityLevel.Name,
                ticket.AssigneeId,
                ticket.CreatorId,
                ticket.RequesterId,
                ticket.CreatedAt,
                ticket.ActualReactionTime,
                ticket.ActualResolutionTime,
                ticket.SlaAccumulatedPauseMinutes,
                ticket.UpdatedAt
            })
            .ToArrayAsync(ct);
        if (tickets.Length == 0) return [];

        var ticketIds = tickets.Select(ticket => ticket.Id).ToArray();
        var closedValue = closedStatusId.Value.ToString(CultureInfo.InvariantCulture);
        var closureTimes = await db.TicketAuditLogs.AsNoTracking()
            .Where(log => ticketIds.Contains(log.TicketId)
                && log.FieldName == "statusId"
                && log.NewValue == closedValue)
            .GroupBy(log => log.TicketId)
            .Select(group => new { TicketId = group.Key, ClosedAt = group.Max(log => log.ChangedAt) })
            .ToDictionaryAsync(item => item.TicketId, item => item.ClosedAt, ct);
        var filtered = tickets.Select(ticket => new
            {
                Ticket = ticket,
                ClosedAt = closureTimes.GetValueOrDefault(
                    ticket.Id,
                    ticket.ActualResolutionTime ?? ticket.UpdatedAt)
            })
            .Where(item => InRange(item.ClosedAt, from, to))
            .ToArray();
        if (filtered.Length == 0) return [];

        var filteredIds = filtered.Select(item => item.Ticket.Id).ToArray();
        var messages = await db.TicketMessages.AsNoTracking()
            .Where(message => filteredIds.Contains(message.TicketId) && message.IsInternal != true)
            .Select(message => new { message.TicketId, message.AuthorId, message.CreatedAt })
            .ToArrayAsync(ct);
        return filtered.Select(item =>
        {
            var ticket = item.Ticket;
            var requesterId = ticket.RequesterId ?? ticket.CreatorId;
            var firstResponse = messages
                .Where(message => message.TicketId == ticket.Id && message.AuthorId != requesterId)
                .Select(message => (DateTimeOffset?)message.CreatedAt)
                .Min();
            return new ClosedTicketRow(
                ticket.Id,
                ticket.TicketNumber,
                ticket.Title,
                ticket.Type,
                ticket.Criticality,
                ticket.AssigneeId,
                ticket.CreatedAt,
                ticket.ActualReactionTime ?? firstResponse,
                ticket.ActualResolutionTime,
                ticket.SlaAccumulatedPauseMinutes,
                item.ClosedAt);
        }).ToList();
    }

    private async Task<ReportDetailResponse> Returns(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var events = await GetReturnEvents(from, to, ct);
        var ticketIds = events.Select(item => item.TicketId).Distinct().ToArray();
        var currentAssignees = ticketIds.Length == 0
            ? new Dictionary<int, int?>()
            : await db.Tickets.AsNoTracking()
                .Where(ticket => ticketIds.Contains(ticket.Id))
                .ToDictionaryAsync(ticket => ticket.Id, ticket => ticket.AssigneeId, ct);
        var history = ticketIds.Length == 0
            ? []
            : await db.TicketAuditLogs.AsNoTracking()
                .Where(log => ticketIds.Contains(log.TicketId) && log.FieldName == "assigneeId")
                .OrderBy(log => log.ChangedAt)
                .Select(log => new AssignmentAuditRow(log.TicketId, log.NewValue, log.ChangedAt))
                .ToArrayAsync(ct);

        var groups = events.Select(item =>
        {
            var historyForTicket = history.Where(log => log.TicketId == item.TicketId).ToArray();
            var assignment = historyForTicket
                .Where(log => log.ChangedAt <= item.ReturnedAt)
                .MaxBy(log => log.ChangedAt);
            int? assigneeId = assignment is null
                ? historyForTicket.Length == 0 ? currentAssignees.GetValueOrDefault(item.TicketId) : null
                : int.TryParse(assignment.NewValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                    ? id
                    : null;
            return assigneeId;
        })
            .GroupBy(assigneeId => assigneeId)
            .Select(group => new { AssigneeId = group.Key, Count = group.Count() })
            .ToArray();
        var names = await GetUserNames(groups.Where(group => group.AssigneeId.HasValue)
            .Select(group => group.AssigneeId!.Value), ct);
        var rows = groups.Select(group => new
            {
                Assignee = group.AssigneeId is int id
                    ? names.GetValueOrDefault(id, "Неизвестный исполнитель")
                    : "Не назначено",
                group.Count
            })
            .OrderBy(item => item.Assignee == "Не назначено" ? 1 : 0)
            .ThenBy(item => item.Assignee)
            .ToArray();

        return Result(
            [new("assignee", "Исполнитель"), new("count", "Возвратов с проверки")],
            rows.Skip(skip).Take(take).Select(item => Row(
                ("assignee", item.Assignee), ("count", Number(item.Count)))),
            rows.Length, skip, take);
    }

    private async Task<List<ReturnEventRow>> GetReturnEvents(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var statuses = await db.TicketStatuses.AsNoTracking()
            .Where(status => status.Code == "review" || status.Code == "pending")
            .Select(status => new { status.Code, status.Id })
            .ToArrayAsync(ct);
        var reviewId = statuses.FirstOrDefault(status => status.Code == "review")?.Id;
        var pendingId = statuses.FirstOrDefault(status => status.Code == "pending")?.Id;
        if (reviewId is null || pendingId is null) return [];

        var oldValue = reviewId.Value.ToString(CultureInfo.InvariantCulture);
        var newValue = pendingId.Value.ToString(CultureInfo.InvariantCulture);
        var events = await db.TicketAuditLogs.AsNoTracking()
            .Where(log => log.FieldName == "statusId"
                && log.OldValue == oldValue
                && log.NewValue == newValue
                && log.Ticket.TenantId == db.GetCurrentTenantId())
            .Select(log => new ReturnEventRow(log.TicketId, log.ChangedAt))
            .ToArrayAsync(ct);
        return events.Where(item => InRange(item.ReturnedAt, from, to)).ToList();
    }

    private async Task<ReportDetailResponse> Repairs(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var records = await GetRepairRecords(from, to, ct);
        var groups = records
            .GroupBy(item => new { item.AssetClassId, item.AssetId, item.Outcome })
            .Select(group => new
            {
                AssetClass = group.First().AssetClass,
                Asset = group.First().Asset,
                group.Key.Outcome,
                Count = group.Count()
            })
            .OrderBy(item => item.AssetClass)
            .ThenBy(item => item.Asset)
            .ThenBy(item => item.Outcome)
            .ToArray();
        var outcomeCounts = records
            .GroupBy(item => item.Outcome)
            .ToDictionary(group => group.Key, group => group.Count());

        return Result(
            [
                new("assetClass", "Класс актива"),
                new("asset", "Наименование актива"),
                new("outcome", "Итог ремонта"),
                new("count", "Количество")
            ],
            groups.Skip(skip).Take(take).Select(item => Row(
                ("assetClass", item.AssetClass),
                ("asset", item.Asset),
                ("outcome", item.Outcome),
                ("count", Number(item.Count)))),
            groups.Length, skip, take, outcomeCounts);
    }

    private async Task<List<RepairRecord>> GetRepairRecords(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var repairTicketIds = await db.Tickets.AsNoTracking()
            .Where(ticket => ticket.Status.Code == ClosedStatusCode && ticket.Type.Code == "repair")
            .Select(ticket => ticket.Id)
            .ToArrayAsync(ct);
        if (repairTicketIds.Length == 0) return [];

        var logs = await db.AssetHistoryLogs.AsNoTracking()
            .Where(log => log.ActionType == AssetHistoryActionTypes.TicketClosed
                && log.TicketId.HasValue
                && repairTicketIds.Contains(log.TicketId.Value))
            .Include(log => log.Asset)
                .ThenInclude(asset => asset.AssetClass)
            .ToArrayAsync(ct);
        return logs.Where(log => InRange(log.ChangedAt, from, to))
            .Select(log => new RepairRecord(
                log.Asset.AssetClassId,
                log.Asset.Id,
                log.Asset.AssetClass?.Name ?? "Без класса",
                log.Asset.Name,
                RepairOutcomeName(ReadRepairOutcome(log.Details))))
            .ToList();
    }

    private async Task<ReportDetailResponse> AssetStages(int skip, int take, CancellationToken ct)
    {
        var assets = await db.Assets.AsNoTracking()
            .Select(asset => new { asset.AssetClassId, asset.LifecycleStage })
            .ToArrayAsync(ct);
        var classes = await db.AssetClasses.AsNoTracking()
            .OrderBy(assetClass => assetClass.Name)
            .Select(assetClass => new { assetClass.Id, assetClass.Name })
            .ToArrayAsync(ct);
        var counts = assets.GroupBy(asset => (asset.AssetClassId, asset.LifecycleStage))
            .ToDictionary(group => group.Key, group => group.Count());
        var classIds = classes.Select(assetClass => (int?)assetClass.Id).ToHashSet();
        var rows = new List<AssetStageRow>();

        foreach (var assetClass in classes)
        {
            var stageCounts = AssetLifecycleStages.All.ToDictionary(
                stage => stage,
                stage => counts.GetValueOrDefault(((int?)assetClass.Id, stage)));
            rows.Add(new AssetStageRow(assetClass.Name, stageCounts));
        }

        var unclassified = assets.Where(asset =>
            asset.AssetClassId is null || !classIds.Contains(asset.AssetClassId)).ToArray();
        if (unclassified.Length > 0)
        {
            var stageCounts = AssetLifecycleStages.All.ToDictionary(
                stage => stage,
                stage => unclassified.Count(asset => asset.LifecycleStage == stage));
            rows.Add(new AssetStageRow("Без класса", stageCounts));
        }

        var columns = new List<ReportColumn> { new("assetClass", "Класс актива") };
        columns.AddRange(AssetLifecycleStages.All.Select(stage =>
            new ReportColumn(stage, AssetLifecycleStages.GetRussianName(stage))));
        return Result(
            columns,
            rows.Skip(skip).Take(take).Select(item =>
            {
                var values = new List<(string Key, string Value)> { ("assetClass", item.AssetClass) };
                values.AddRange(AssetLifecycleStages.All.Select(stage => (stage, Number(item.Counts[stage]))));
                return Row(values.ToArray());
            }),
            rows.Count, skip, take);
    }

    private async Task<ReportDetailResponse> Movements(DateOnly? from, DateOnly? to, int skip, int take, CancellationToken ct)
    {
        var query = FilterMovements(db.AssetMovements.AsNoTracking(), from, to);
        var total = await query.CountAsync(ct);
        var movements = await query
            .Include(item => item.Asset)
            .Include(item => item.FromUser)
            .Include(item => item.ToUser)
            .Include(item => item.PerformedBy)
            .OrderByDescending(item => item.PerformedAt)
            .ThenByDescending(item => item.Id)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(ct);
        var valueIds = movements.SelectMany(item => new[]
            {
                item.FromWarehouseId, item.ToWarehouseId,
                item.FromLocationId, item.ToLocationId,
                item.FromDepartmentId, item.ToDepartmentId
            })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var dictionaryValues = valueIds.Length == 0
            ? []
            : await db.DictionaryValues.AsNoTracking()
                .Where(value => valueIds.Contains(value.Id)
                    && !value.IsArchived
                    && !value.Dictionary.IsArchived
                    && value.Dictionary.TenantId == db.GetCurrentTenantId())
                .Select(value => new { value.Id, value.Name, DictionaryCode = value.Dictionary.Code })
                .ToArrayAsync(ct);
        var valuesByKey = dictionaryValues.ToDictionary(
            item => (item.Id, item.DictionaryCode),
            item => item.Name);

        return Result(
            [
                new("date", "Дата и время"),
                new("asset", "Актив"),
                new("type", "Тип движения"),
                new("from", "Откуда"),
                new("to", "Куда"),
                new("actor", "Провел операцию")
            ],
            movements.Select(item => Row(
                ("date", FormatDate(item.PerformedAt)),
                ("asset", $"{item.Asset.Name} ({item.Asset.InventoryNumber})"),
                ("type", AssetMovementTypes.GetRussianName(item.MovementType)),
                ("from", MovementPlace(
                    item.FromUser?.FullName,
                    GetDictionaryValue(valuesByKey, item.FromWarehouseId, "warehouses"),
                    GetDictionaryValue(valuesByKey, item.FromLocationId, "locations"),
                    GetDictionaryValue(valuesByKey, item.FromDepartmentId, "departments"))),
                ("to", MovementPlace(
                    item.ToUser?.FullName,
                    GetDictionaryValue(valuesByKey, item.ToWarehouseId, "warehouses"),
                    GetDictionaryValue(valuesByKey, item.ToLocationId, "locations"),
                    GetDictionaryValue(valuesByKey, item.ToDepartmentId, "departments"))),
                ("actor", item.PerformedBy.FullName))),
            total, skip, take);
    }

    private IQueryable<Ticket> FilterTickets(IQueryable<Ticket> query, DateOnly? from, DateOnly? to)
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

    private async Task<Dictionary<int, string>> GetUserNames(IEnumerable<int> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        return await db.Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id) && user.TenantId == db.GetCurrentTenantId())
            .ToDictionaryAsync(user => user.Id, user => user.FullName, ct);
    }

    private static string? ReadRepairOutcome(string? details)
    {
        if (string.IsNullOrWhiteSpace(details)) return null;
        using var document = JsonDocument.Parse(details);
        return document.RootElement.TryGetProperty("repairOutcome", out var value)
            ? value.GetString()
            : null;
    }

    private static string RepairOutcomeName(string? outcome) => outcome switch
    {
        "ReturnToExploitation" => "В эксплуатацию",
        "TransferToWarehouse" => "На склад",
        "Retire" => "Списать",
        _ => "Не указан"
    };

    private static string? GetDictionaryValue(
        IReadOnlyDictionary<(int Id, string DictionaryCode), string> values,
        int? id,
        string dictionaryCode) =>
        id is int valueId && values.TryGetValue((valueId, dictionaryCode), out var name) ? name : null;

    private static string MovementPlace(params string?[] parts)
    {
        var present = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
        return present.Length == 0 ? "—" : string.Join(", ", present);
    }

    private static bool InRange(DateTimeOffset value, DateOnly? from, DateOnly? to) =>
        (from is not DateOnly start || value >= StartOfDay(start))
        && (to is not DateOnly end || value < StartOfDay(end.AddDays(1)));

    private static DateTimeOffset StartOfDay(DateOnly day) =>
        new(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

    private static TimeSpan NonNegative(TimeSpan duration) =>
        duration < TimeSpan.Zero ? TimeSpan.Zero : duration;

    private static TimeSpan? Average(IEnumerable<TimeSpan?> values)
    {
        var durations = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return durations.Length == 0
            ? null
            : TimeSpan.FromTicks((long)durations.Average(duration => duration.Ticks));
    }

    private static string FormatDuration(TimeSpan? value) => value is not TimeSpan duration
        ? "—"
        : $"{(long)duration.TotalHours:00}:{duration.Minutes:00}";

    private static string FormatDate(DateTimeOffset value) =>
        value.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    private static string Percent(int value, int total) =>
        total == 0 ? "0%" : $"{(value * 100d / total).ToString("0.##", CultureInfo.InvariantCulture)}%";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static ReportDetailResponse Result(
        IReadOnlyList<ReportColumn> columns,
        IEnumerable<ReportDetailRow> items,
        int total,
        int skip,
        int take,
        IReadOnlyDictionary<string, int>? aggregates = null) =>
        new(columns, items.ToArray(), total, skip, take, aggregates);

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

    private sealed record SlaTicketRow(
        int TypeId,
        int? CriticalityId,
        string Type,
        string Criticality,
        bool ReactionBreached,
        bool ResolutionBreached);
    private sealed record ClosedTicketRow(
        int Id,
        string TicketNumber,
        string Title,
        string Type,
        string Criticality,
        int? AssigneeId,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ReactionAt,
        DateTimeOffset? ActualResolutionAt,
        int SlaAccumulatedPauseMinutes,
        DateTimeOffset ClosedAt);
    private sealed record ReturnEventRow(int TicketId, DateTimeOffset ReturnedAt);
    private sealed record AssignmentAuditRow(int TicketId, string? NewValue, DateTimeOffset ChangedAt);
    private sealed record RepairRecord(int? AssetClassId, int AssetId, string AssetClass, string Asset, string Outcome);
    private sealed record AssetStageRow(string AssetClass, Dictionary<string, int> Counts);
}

using System.Text.Json;
using itsm.Domain.Entities;

namespace itsm.Infrastructure.Persistence.Services;

public sealed record WorkingIntervalSnapshot(TimeOnly StartTime, TimeOnly EndTime);

public sealed record CalendarDateOverride(
    DateOnly Date,
    bool IsWorkDay,
    TimeOnly? StartTime,
    TimeOnly? EndTime);

public sealed record WorkingCalendarSnapshot(
    int Version,
    string TimeZoneId,
    bool Is24x7,
    Dictionary<int, WorkingIntervalSnapshot[]> Weekly,
    Dictionary<DateOnly, CalendarDateOverride> Exceptions);

public static class WorkingTimeCalculator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int SearchLimitDays = 3660;

    public static WorkingCalendarSnapshot CreateSnapshot(
        WorkingSchedule schedule,
        IEnumerable<WorkingHour> hours,
        IEnumerable<CalendarException> exceptions)
    {
        var exceptionMap = exceptions
            .GroupBy(item => item.ExceptionDate)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var item = group.OrderBy(exception => exception.WorkingScheduleId.HasValue).Last();
                    return new CalendarDateOverride(
                        item.ExceptionDate,
                        item.IsWorkDay,
                        item.StartTime,
                        item.EndTime);
                });

        var weekly = hours
            .GroupBy(item => item.DayOfWeek)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(item => item.StartTime)
                    .Select(item => new WorkingIntervalSnapshot(item.StartTime, item.EndTime))
                    .ToArray());

        var snapshot = new WorkingCalendarSnapshot(
            schedule.Version,
            schedule.TimeZoneId,
            string.Equals(schedule.ScheduleType, "24x7", StringComparison.OrdinalIgnoreCase),
            weekly,
            exceptionMap);
        Validate(snapshot);
        return snapshot;
    }

    public static string Serialize(WorkingCalendarSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, JsonOptions);

    public static WorkingCalendarSnapshot Deserialize(string json) =>
        JsonSerializer.Deserialize<WorkingCalendarSnapshot>(json, JsonOptions)
        ?? throw new InvalidOperationException("Снимок календаря SLA поврежден.");

    public static DateTimeOffset AddWorkingMinutes(
        DateTimeOffset start,
        int minutes,
        WorkingCalendarSnapshot calendar)
    {
        if (minutes <= 0)
            return start;

        Validate(calendar);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId);
        var current = start;
        var remaining = TimeSpan.FromMinutes(minutes);

        for (var searchedDays = 0; searchedDays <= SearchLimitDays; searchedDays++)
        {
            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(current, zone).DateTime);
            foreach (var interval in GetIntervals(localDate, calendar))
            {
                var (intervalStart, intervalEnd) = GetInstants(localDate, interval, zone);
                var cursor = current > intervalStart ? current : intervalStart;
                if (cursor >= intervalEnd)
                    continue;

                var available = intervalEnd - cursor;
                if (remaining <= available)
                    return cursor.Add(remaining);

                remaining -= available;
                current = intervalEnd;
            }

            var nextDate = localDate.AddDays(1);
            if (searchedDays == SearchLimitDays)
                break;

            current = StartOfDate(nextDate, zone);
        }

        throw new InvalidOperationException("В календаре SLA не найден рабочий интервал в течение 10 лет.");
    }

    public static int WorkingMinutesBetween(
        DateTimeOffset start,
        DateTimeOffset end,
        WorkingCalendarSnapshot calendar)
    {
        if (end <= start)
            return 0;

        Validate(calendar);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId);
        var firstDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, zone).DateTime).AddDays(-1);
        var lastDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(end, zone).DateTime).AddDays(1);
        long totalTicks = 0;

        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            foreach (var interval in GetIntervals(date, calendar))
            {
                var (intervalStart, intervalEnd) = GetInstants(date, interval, zone);
                var overlapStart = start > intervalStart ? start : intervalStart;
                var overlapEnd = end < intervalEnd ? end : intervalEnd;
                if (overlapEnd > overlapStart)
                    totalTicks += (overlapEnd - overlapStart).Ticks;
            }
        }

        return checked((int)(totalTicks / TimeSpan.TicksPerMinute));
    }

    private static IReadOnlyList<WorkingIntervalSnapshot> GetIntervals(
        DateOnly date,
        WorkingCalendarSnapshot calendar)
    {
        if (calendar.Exceptions.TryGetValue(date, out var exception))
        {
            if (!exception.IsWorkDay)
                return [];
            if (exception.StartTime is TimeOnly start && exception.EndTime is TimeOnly end)
                return [new WorkingIntervalSnapshot(start, end)];
        }

        if (calendar.Is24x7)
            return [new WorkingIntervalSnapshot(TimeOnly.MinValue, TimeOnly.MaxValue)];

        return calendar.Weekly.TryGetValue((int)date.DayOfWeek, out var intervals)
            ? intervals
            : [];
    }

    private static (DateTimeOffset Start, DateTimeOffset End) GetInstants(
        DateOnly date,
        WorkingIntervalSnapshot interval,
        TimeZoneInfo zone)
    {
        var start = ToInstant(date, interval.StartTime, zone, isStartBoundary: true);
        DateTimeOffset end;
        if (interval.EndTime == TimeOnly.MaxValue && interval.StartTime == TimeOnly.MinValue)
        {
            end = StartOfDate(date.AddDays(1), zone);
        }
        else
        {
            end = ToInstant(date, interval.EndTime, zone, isStartBoundary: false);
        }

        if (end <= start)
            throw new InvalidOperationException("В интервале рабочего календаря конец должен быть позже начала.");
        return (start, end);
    }

    private static DateTimeOffset StartOfDate(DateOnly date, TimeZoneInfo zone) =>
        ToInstant(date, TimeOnly.MinValue, zone, isStartBoundary: true);

    private static DateTimeOffset ToInstant(
        DateOnly date,
        TimeOnly time,
        TimeZoneInfo zone,
        bool isStartBoundary)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local))
            local = local.AddMinutes(1);

        var offset = zone.IsAmbiguousTime(local)
            ? isStartBoundary
                ? zone.GetAmbiguousTimeOffsets(local).Max()
                : zone.GetAmbiguousTimeOffsets(local).Min()
            : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    private static void Validate(WorkingCalendarSnapshot calendar)
    {
        _ = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId);
        if (calendar.Weekly.Keys.Any(day => day is < 0 or > 6))
            throw new InvalidOperationException("День недели в календаре SLA должен быть в диапазоне от 0 до 6.");
        foreach (var intervals in calendar.Weekly.Values)
            ValidateIntervals(intervals);
        foreach (var exception in calendar.Exceptions.Values)
        {
            if (exception.StartTime.HasValue != exception.EndTime.HasValue)
                throw new InvalidOperationException("Исключение календаря должно содержать обе границы рабочего интервала.");
            if (exception.StartTime is TimeOnly start && exception.EndTime is TimeOnly end && end <= start)
                throw new InvalidOperationException("Конец рабочего интервала исключения должен быть позже начала.");
        }
    }

    private static void ValidateIntervals(IEnumerable<WorkingIntervalSnapshot> intervals)
    {
        TimeOnly? previousEnd = null;
        foreach (var interval in intervals.OrderBy(item => item.StartTime))
        {
            if (interval.EndTime <= interval.StartTime || (previousEnd.HasValue && interval.StartTime < previousEnd))
                throw new InvalidOperationException("Интервалы рабочего календаря должны быть корректными и не пересекаться.");
            previousEnd = interval.EndTime;
        }
    }
}

using System.Text.Json;
using itsm.Domain.Entities;

namespace itsm.Infrastructure.Persistence.Services;

public sealed record SlaWorkingInterval(int StartMinute, int EndMinute);

public sealed record SlaCalendarWeekDay(int DayOfWeek, IReadOnlyList<SlaWorkingInterval> Intervals);

public sealed record SlaCalendarDateOverride(
    DateOnly Date,
    bool IsWorkDay,
    IReadOnlyList<SlaWorkingInterval>? Intervals);

public sealed record SlaCalendarSnapshot(
    string TimeZoneId,
    string ScheduleType,
    int Version,
    IReadOnlyList<SlaCalendarWeekDay> WeeklyHours,
    IReadOnlyList<SlaCalendarDateOverride> Exceptions);

public static class SlaCalendarCalculator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int SearchLimitDays = 36600;

    public static SlaCalendarSnapshot CreateSnapshot(
        WorkingSchedule schedule,
        IEnumerable<WorkingHour> hours,
        IEnumerable<CalendarException> exceptions)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId);
        if (schedule.Version <= 0)
            throw new InvalidOperationException("Версия календаря SLA должна быть положительной.");
        var weeklyHours = hours
            .GroupBy(hour => hour.DayOfWeek)
            .Select(group =>
            {
                if (group.Key is < 0 or > 6)
                    throw new InvalidOperationException($"Некорректный день недели в расписании SLA: {group.Key}.");
                if (group.Any(hour =>
                    hour.StartTime.Ticks % TimeSpan.TicksPerMinute != 0
                    || hour.EndTime.Ticks % TimeSpan.TicksPerMinute != 0))
                    throw new InvalidOperationException("Время начала и окончания рабочего интервала задается с точностью до минуты.");
                return new SlaCalendarWeekDay(
                    group.Key,
                    NormalizeIntervals(group.Select(hour => new SlaWorkingInterval(
                        hour.StartTime.Hour * 60 + hour.StartTime.Minute,
                        hour.EndTime.Hour * 60 + hour.EndTime.Minute))));
            })
            .OrderBy(day => day.DayOfWeek)
            .ToArray();

        var dateOverrides = exceptions
            .OrderBy(item => item.ExceptionDate)
            .Select(item => new SlaCalendarDateOverride(
                item.ExceptionDate,
                item.IsWorkDay,
                item.WorkingHoursJson is null
                    ? null
                    : NormalizeIntervals(
                        JsonSerializer.Deserialize<List<SlaWorkingInterval>>(item.WorkingHoursJson, JsonOptions)
                        ?? throw new InvalidOperationException(
                            $"Некорректные рабочие интервалы для исключения {item.ExceptionDate:yyyy-MM-dd}."))))
            .ToArray();

        if (!string.Equals(schedule.ScheduleType, "24x7", StringComparison.OrdinalIgnoreCase)
            && weeklyHours.Length == 0)
            throw new InvalidOperationException($"В расписании SLA «{schedule.Name}» не заданы рабочие часы.");

        return new SlaCalendarSnapshot(
            timeZone.Id,
            schedule.ScheduleType,
            schedule.Version,
            weeklyHours,
            dateOverrides);
    }

    public static string Serialize(SlaCalendarSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, JsonOptions);

    public static SlaCalendarSnapshot Deserialize(string json) =>
        JsonSerializer.Deserialize<SlaCalendarSnapshot>(json, JsonOptions)
        ?? throw new InvalidOperationException("Не удалось прочитать snapshot календаря SLA.");

    public static DateTimeOffset AddWorkingMinutes(
        DateTimeOffset start,
        long minutes,
        SlaCalendarSnapshot calendar) =>
        AddWorkingTime(start, TimeSpan.FromMinutes(minutes), calendar);

    public static DateTimeOffset AddWorkingTime(
        DateTimeOffset start,
        TimeSpan duration,
        SlaCalendarSnapshot calendar)
    {
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "Длительность не может быть отрицательной.");
        if (duration == TimeSpan.Zero)
            return start;

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId);
        var cursor = start.ToUniversalTime();
        var remaining = duration;
        var intervalCount = 0;

        while (remaining > TimeSpan.Zero)
        {
            var interval = FindNextInterval(cursor, calendar, timeZone);
            if (interval is null || ++intervalCount > SearchLimitDays * 24)
                throw new InvalidOperationException("В календаре SLA не удалось найти следующий рабочий интервал.");

            cursor = interval.Value.Start;
            var available = interval.Value.End - cursor;
            if (remaining <= available)
                return cursor.Add(remaining);

            remaining -= available;
            cursor = interval.Value.End;
        }

        return cursor;
    }

    public static TimeSpan WorkingTimeBetween(
        DateTimeOffset start,
        DateTimeOffset end,
        SlaCalendarSnapshot calendar)
    {
        if (end <= start)
            return TimeSpan.Zero;

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId);
        var localStart = TimeZoneInfo.ConvertTime(start, timeZone).Date.AddDays(-1);
        var localEnd = TimeZoneInfo.ConvertTime(end, timeZone).Date.AddDays(1);
        var total = TimeSpan.Zero;

        for (var date = DateOnly.FromDateTime(localStart);
             date <= DateOnly.FromDateTime(localEnd);
             date = date.AddDays(1))
        {
            foreach (var interval in GetIntervals(date, calendar))
            {
                var intervalStart = ToUtc(date, interval.StartMinute, timeZone, isStart: true);
                var intervalEnd = ToUtc(date, interval.EndMinute, timeZone, isStart: false);
                var overlapStart = start > intervalStart ? start : intervalStart;
                var overlapEnd = end < intervalEnd ? end : intervalEnd;
                if (overlapEnd > overlapStart)
                    total += overlapEnd - overlapStart;
            }
        }

        return total;
    }

    private static (DateTimeOffset Start, DateTimeOffset End)? FindNextInterval(
        DateTimeOffset instant,
        SlaCalendarSnapshot calendar,
        TimeZoneInfo timeZone)
    {
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, timeZone).Date);
        for (var offset = 0; offset <= SearchLimitDays; offset++)
        {
            var date = localDate.AddDays(offset);
            foreach (var interval in GetIntervals(date, calendar))
            {
                var start = ToUtc(date, interval.StartMinute, timeZone, isStart: true);
                var end = ToUtc(date, interval.EndMinute, timeZone, isStart: false);
                var candidate = instant > start ? instant : start;
                if (candidate < end)
                    return (candidate, end);
            }
        }

        return null;
    }

    private static IReadOnlyList<SlaWorkingInterval> GetIntervals(
        DateOnly date,
        SlaCalendarSnapshot calendar)
    {
        var exception = calendar.Exceptions.FirstOrDefault(item => item.Date == date);
        if (exception is not null)
        {
            if (!exception.IsWorkDay)
                return [];
            if (exception.Intervals is not null)
                return exception.Intervals;
        }

        if (string.Equals(calendar.ScheduleType, "24x7", StringComparison.OrdinalIgnoreCase))
            return [new SlaWorkingInterval(0, 1440)];

        return calendar.WeeklyHours
            .FirstOrDefault(item => item.DayOfWeek == (int)date.DayOfWeek)
            ?.Intervals ?? [];
    }

    private static DateTimeOffset ToUtc(
        DateOnly date,
        int minute,
        TimeZoneInfo timeZone,
        bool isStart)
    {
        var localDate = minute == 1440 ? date.AddDays(1) : date;
        var local = localDate.ToDateTime(TimeOnly.MinValue).AddMinutes(minute == 1440 ? 0 : minute);

        while (timeZone.IsInvalidTime(local))
            local = local.AddMinutes(1);

        TimeSpan offset;
        if (timeZone.IsAmbiguousTime(local))
        {
            var offsets = timeZone.GetAmbiguousTimeOffsets(local).OrderBy(value => value).ToArray();
            offset = offsets[isStart ? offsets.Length - 1 : 0];
        }
        else
        {
            offset = timeZone.GetUtcOffset(local);
        }
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    private static IReadOnlyList<SlaWorkingInterval> NormalizeIntervals(IEnumerable<SlaWorkingInterval> intervals)
    {
        var normalized = intervals.OrderBy(interval => interval.StartMinute).ToArray();
        for (var index = 0; index < normalized.Length; index++)
        {
            var current = normalized[index];
            if (current.StartMinute < 0 || current.StartMinute >= current.EndMinute || current.EndMinute > 1440)
                throw new InvalidOperationException("Рабочий интервал SLA должен лежать в пределах дня и иметь положительную длину.");
            if (index > 0 && normalized[index - 1].EndMinute > current.StartMinute)
                throw new InvalidOperationException("Рабочие интервалы SLA не должны пересекаться.");
        }

        return normalized;
    }
}

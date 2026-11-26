namespace itsm.Domain.Sla;

/// <summary>Событие статуса заявки для пересчёта SLA.</summary>
public readonly record struct StatusEvent(DateTimeOffset At, StatusKind Kind);

/// <summary>Классификация статуса относительно SLA-таймера.</summary>
public enum StatusKind
{
    /// <summary>Таймер идёт.</summary>
    Active = 0,

    /// <summary>Таймер замораживается, копится рабочее время паузы.</summary>
    Pause = 1,

    /// <summary>Терминальный статус: SLA больше не тикает.</summary>
    Stop = 2
}

public sealed record SlaDeadlineResult(
    DateTimeOffset BaseDeadline,
    DateTimeOffset Deadline,
    DateTimeOffset? BreachAt,
    bool IsPaused)
{
    /// <summary>Просрочено ли на указанный момент.</summary>
    public bool IsBreachedAt(DateTimeOffset moment) =>
        BreachAt is not null || (!IsPaused && moment >= Deadline);

    /// <summary>Момент просрочки с учётом текущего состояния.</summary>
    public DateTimeOffset? EffectiveBreach(DateTimeOffset moment) =>
        BreachAt ?? (!IsPaused && moment >= Deadline ? Deadline : null);
}

/// <summary>
/// Ядро SLA: расчёт дедлайнов в рабочих минутах по календарю тенанта.
/// Реализация строго по спецификации (add_working_minutes / working_minutes_between / calculate_sla_deadline).
/// </summary>
public static class SlaCalculator
{
    /// <summary>Добавляет рабочие минуты к моменту времени.</summary>
    public static DateTimeOffset AddWorkingMinutes(DateTimeOffset start, int minutes, SlaCalendar calendar)
    {
        if (minutes <= 0)
            return start;

        var remaining = TimeSpan.FromMinutes(minutes);
        var cursor = NextWorkingInstant(start, calendar);

        while (true)
        {
            var (date, interval) = WorkingIntervalContaining(cursor, calendar);

            // Между интервалами курсор должен стоять в начале интервала, поэтому проверка выше всегда успешна.
            var intervalEnd = ToInstant(date, interval.End, calendar);
            var available = intervalEnd - cursor;

            if (remaining <= available)
                return cursor + remaining;

            remaining -= available;
            cursor = NextWorkingInstant(intervalEnd, calendar);
        }
    }

    /// <summary>Считает рабочие минуты в полуинтервале [from, to).</summary>
    public static int WorkingMinutesBetween(DateTimeOffset from, DateTimeOffset to, SlaCalendar calendar)
    {
        if (to <= from)
            return 0;

        var timeZone = calendar.TimeZone;
        var firstDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(from, timeZone).DateTime).AddDays(-1);
        var lastDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(to, timeZone).DateTime).AddDays(1);

        var totalMinutes = 0d;
        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            foreach (var interval in calendar.IntervalsFor(date))
            {
                var intervalStart = ToInstant(date, interval.Start, calendar);
                var intervalEnd = ToInstant(date, interval.End, calendar);

                var overlapStart = from > intervalStart ? from : intervalStart;
                var overlapEnd = to < intervalEnd ? to : intervalEnd;
                if (overlapEnd > overlapStart)
                    totalMinutes += (overlapEnd - overlapStart).TotalMinutes;
            }
        }

        // Календарь тенанта для DST-переходов: используем округление до целых минут.
        return (int)Math.Floor(totalMinutes);
    }

    /// <summary>Ближайший рабочий момент: сам момент, если он рабочий, иначе начало следующего интервала.</summary>
    public static DateTimeOffset NextWorkingInstant(DateTimeOffset moment, SlaCalendar calendar)
{
    var timeZone = calendar.TimeZone;
    var localMoment = TimeZoneInfo.ConvertTime(moment, timeZone);
    var date = DateOnly.FromDateTime(localMoment.DateTime);
    var timeOfDay = TimeOnly.FromDateTime(localMoment.DateTime);

    for (var offset = 0; offset <= 14; offset++)
    {
        var intervals = calendar.IntervalsFor(date);
        for (var index = 0; index < intervals.Count; index++)
        {
            var interval = intervals[index];
            if (offset == 0 && timeOfDay >= interval.Start && timeOfDay < interval.End)
                return moment;

            var intervalStart = ToInstant(date, interval.Start, calendar);
            if (offset > 0 || timeOfDay < interval.Start)
                return intervalStart;
        }

        date = date.AddDays(1);
        timeOfDay = TimeOnly.MinValue;
    }

    throw new InvalidOperationException(
        "Рабочий календарь не содержит ни одного рабочего интервала в ближайшие две недели.");
}

    public static SlaDeadlineResult CalculateDeadline(
        DateTimeOffset createdAt,
        int durationMinutes,
        SlaCalendar calendar,
        IEnumerable<StatusEvent> statusEvents,
        DateTimeOffset? evaluatedAt = null)
    {
        if (durationMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes), "SLA duration must be greater than zero.");

        var baseDeadline = AddWorkingMinutes(createdAt, durationMinutes, calendar);
        var deadline = baseDeadline;
        DateTimeOffset? breachAt = null;
        var isPaused = false;
        DateTimeOffset pauseStart = default;

        foreach (var statusEvent in statusEvents.OrderBy(item => item.At))
        {
            if (!isPaused && breachAt is null && statusEvent.At >= deadline)
                breachAt = deadline;

            switch (statusEvent.Kind)
            {
                case StatusKind.Pause:
                    if (!isPaused)
                    {
                        isPaused = true;
                        pauseStart = statusEvent.At;
                    }

                    break;

                case StatusKind.Active:
                    if (isPaused)
                    {
                        ApplyPause(calendar, pauseStart, statusEvent.At, ref deadline, breachAt);
                        isPaused = false;
                    }

                    break;

                case StatusKind.Stop:
                    if (isPaused)
                    {
                        ApplyPause(calendar, pauseStart, statusEvent.At, ref deadline, breachAt);
                        isPaused = false;
                    }

                    return new SlaDeadlineResult(baseDeadline, deadline, breachAt, IsPaused: false);
            }
        }

        if (!isPaused && breachAt is null)
        {
            var reference = evaluatedAt ?? DateTimeOffset.UtcNow;
            if (reference >= deadline)
                breachAt = deadline;
        }

        return new SlaDeadlineResult(baseDeadline, deadline, breachAt, isPaused);
    }

    private static void ApplyPause(
        SlaCalendar calendar,
        DateTimeOffset pauseStart,
        DateTimeOffset pauseEnd,
        ref DateTimeOffset deadline,
        DateTimeOffset? breachAt)
    {
        var pauseWork = WorkingMinutesBetween(pauseStart, pauseEnd, calendar);

        // Пауза после breach не отменяет просрочку и не сдвигает дедлайн.
        if (breachAt is null && pauseWork > 0 && pauseStart < deadline)
            deadline = AddWorkingMinutes(deadline, pauseWork, calendar);
    }

    private static (DateOnly Date, WorkingInterval Interval) WorkingIntervalContaining(
        DateTimeOffset moment,
        SlaCalendar calendar)
    {
        var timeZone = calendar.TimeZone;
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, timeZone).DateTime);

        foreach (var interval in calendar.IntervalsFor(localDate))
        {
            var intervalStart = ToInstant(localDate, interval.Start, calendar);
            var intervalEnd = ToInstant(localDate, interval.End, calendar);
            if (moment >= intervalStart && moment < intervalEnd)
                return (localDate, interval);
        }

        throw new InvalidOperationException("Момент времени не попадает в рабочий интервал.");
    }

    /// <summary>Переводит локальное время тенанта в абсолютный момент.</summary>
    private static DateTimeOffset ToInstant(DateOnly date, TimeOnly time, SlaCalendar calendar)
    {
        var timeZone = calendar.TimeZone;
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        var offset = timeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}

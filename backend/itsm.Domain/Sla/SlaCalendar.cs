namespace itsm.Domain.Sla;

/// <summary>Полуоткрытый рабочий интервал внутри дня: [Start, End).</summary>
public readonly record struct WorkingInterval(TimeOnly Start, TimeOnly End)
{
    public bool IsValid => End > Start;

    public TimeSpan Duration => End - Start;

    public bool Contains(TimeOnly time) => time >= Start && time < End;

    /// <summary>Полный рабочий день 00:00–24:00 (представлен как 23:59:59.999 + 1 мс).</summary>
    public static WorkingInterval AllDay =>
        new(new TimeOnly(0, 0), new TimeOnly(23, 59, 59, 999));
}

/// <summary>Тип исключения календаря на конкретную дату.</summary>
public enum CalendarExceptionType
{
    /// <summary>Дата выходная, даже если по расписанию это рабочий день.</summary>
    DayOff = 0,

    /// <summary>Дата рабочая с переопределёнными интервалами (например, сокращённый день).</summary>
    WorkingDay = 1
}

/// <summary>Исключение календаря: выходной или рабочий день с интервалами.</summary>
public sealed record CalendarException(DateOnly Date, CalendarExceptionType Type, IReadOnlyList<WorkingInterval> Intervals)
{
    public static CalendarException DayOff(DateOnly date) => new(date, CalendarExceptionType.DayOff, []);

    public static CalendarException Working(DateOnly date, params WorkingInterval[] intervals) =>
        new(date, CalendarExceptionType.WorkingDay, intervals);
}

/// <summary>
/// Рабочий календарь тенанта. Интервалы задаются в локальном времени тенанта,
/// расчёт ведётся по абсолютным моментам, поэтому переход на летнее/зимнее время учитывается.
/// Приоритет: исключение на дату &gt; праздник &gt; обычный день недели.
/// </summary>
public sealed record SlaCalendar(
    string TimeZoneId,
    IReadOnlyDictionary<DayOfWeek, IReadOnlyList<WorkingInterval>> Weekly,
    IReadOnlySet<DateOnly> Holidays,
    IReadOnlyDictionary<DateOnly, CalendarException> Exceptions)
{
    public const string DefaultTimeZoneId = "Europe/Moscow";

    /// <summary>Круглосуточный календарь (24x7).</summary>
    public static SlaCalendar Always24x7(string timeZoneId = DefaultTimeZoneId)
    {
        var weekly = new Dictionary<DayOfWeek, IReadOnlyList<WorkingInterval>>();
        foreach (var day in Enum.GetValues<DayOfWeek>())
            weekly[day] = new[] { WorkingInterval.AllDay };

        return new SlaCalendar(
            timeZoneId,
            weekly,
            new HashSet<DateOnly>(),
            new Dictionary<DateOnly, CalendarException>());
    }

    /// <summary>Пн–Пт с одним интервалом в день.</summary>
    public static SlaCalendar BusinessWeek(
        string timeZoneId,
        WorkingInterval interval,
        IEnumerable<DateOnly>? holidays = null,
        IEnumerable<CalendarException>? exceptions = null)
    {
        var weekly = new Dictionary<DayOfWeek, IReadOnlyList<WorkingInterval>>();
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            weekly[day] = day is DayOfWeek.Saturday or DayOfWeek.Sunday
                ? Array.Empty<WorkingInterval>()
                : new[] { interval };
        }

        return new SlaCalendar(
            timeZoneId,
            weekly,
            (holidays ?? []).ToList().ToHashSet(),
            (exceptions ?? []).ToDictionary(exception => exception.Date));
    }

    /// <summary>Базовый календарь из спецификации: Пн–Пт 09:00–18:00.</summary>
    public static SlaCalendar Base(string timeZoneId = DefaultTimeZoneId) =>
        BusinessWeek(timeZoneId, new WorkingInterval(new TimeOnly(9, 0), new TimeOnly(18, 0)));

    public TimeZoneInfo TimeZone
    {
        get
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }
    }

    /// <summary>Рабочие интервалы конкретной даты с учётом исключений и праздников.</summary>
    public IReadOnlyList<WorkingInterval> IntervalsFor(DateOnly date)
    {
        if (Exceptions.TryGetValue(date, out var exception))
        {
            return exception.Type == CalendarExceptionType.DayOff
                ? []
                : exception.Intervals.Where(interval => interval.IsValid).OrderBy(interval => interval.Start).ToArray();
        }

        if (Holidays.Contains(date))
            return [];

        return Weekly.TryGetValue(date.DayOfWeek, out var intervals)
            ? intervals.Where(interval => interval.IsValid).OrderBy(interval => interval.Start).ToArray()
            : [];
    }
}

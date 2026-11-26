using FluentAssertions;
using itsm.Domain.Sla;

namespace itsm.Tests.UnitTests.Domain;

public class SlaCalculatorTests
{
    // Базовая неделя: 2026-01-19 (Пн) … 2026-01-23 (Пт); 24–25.01 — выходные.
    private static readonly DateOnly Monday = new(2026, 1, 19);
    private static readonly DateOnly Tuesday = new(2026, 1, 20);
    private static readonly DateOnly Wednesday = new(2026, 1, 21);
    private static readonly DateOnly Thursday = new(2026, 1, 22);
    private static readonly DateOnly Friday = new(2026, 1, 23);
    private static readonly DateOnly Saturday = new(2026, 1, 24);
private static readonly DateOnly NextMonday = new(2026, 1, 26);

    private static readonly WorkingInterval Day = new(new TimeOnly(9, 0), new TimeOnly(18, 0));

    private static SlaCalendar BaseCalendar() => SlaCalendar.BusinessWeek(SlaCalendar.DefaultTimeZoneId, Day);

    private static DateTimeOffset At(DateOnly date, int hour, int minute = 0) =>
        new(date.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(3));

    [Fact]
    public void BaseWeekDatesAreStable() =>
        Monday.DayOfWeek.Should().Be(DayOfWeek.Monday);

    private static SlaDeadlineResult Calculate(
        DateTimeOffset createdAt,
        int durationMinutes,
        SlaCalendar calendar,
        params StatusEvent[] events) =>
        SlaCalculator.CalculateDeadline(createdAt, durationMinutes, calendar, events, createdAt);

    [Fact]
    public void TC01_TwoHoursFromMondayMorningEndsSameDay() =>
        SlaCalculator.AddWorkingMinutes(At(Monday, 10), 120, BaseCalendar()).Should().Be(At(Monday, 12));

    [Fact]
    public void TC02_CreatedBeforeEndOfDaySpillsOverToNextWorkingDay() =>
        SlaCalculator.AddWorkingMinutes(At(Monday, 17), 120, BaseCalendar()).Should().Be(At(Tuesday, 10));

    [Fact]
    public void TC03_RemainingMinutesAreNotRoundedToEndOfDay() =>
        SlaCalculator.AddWorkingMinutes(At(Monday, 17, 55), 120, BaseCalendar()).Should().Be(At(Tuesday, 10, 55));

    [Fact]
    public void TC04_CreatedExactlyAtClosingTimeStartsNextWorkingDay() =>
        SlaCalculator.AddWorkingMinutes(At(Monday, 18), 120, BaseCalendar()).Should().Be(At(Tuesday, 11));

    [Fact]
    public void TC05_CreatedOnSaturdayStartsFromMonday() =>
SlaCalculator.AddWorkingMinutes(At(Saturday, 12), 120, BaseCalendar()).Should().Be(At(NextMonday, 11));

    [Fact]
    public void TC06_HolidayOnMondaySkipsToTuesday()
    {
var calendar = SlaCalendar.BusinessWeek(SlaCalendar.DefaultTimeZoneId, Day, holidays: [Monday]);

        // Заявка создана в праздничный Пн 09:00, SLA стартует со Вт 09:00 и заканчивается во Вт 11:00.
        SlaCalculator.AddWorkingMinutes(At(Monday, 9), 120, calendar).Should().Be(At(Tuesday, 11));
    }

    [Fact]
    public void TC07_PauseWithinWorkingHoursShiftsDeadline()
    {
        var result = Calculate(
            At(Monday, 9), 480, BaseCalendar(),
            new StatusEvent(At(Monday, 10), StatusKind.Pause),
            new StatusEvent(At(Monday, 12), StatusKind.Active));

        result.Deadline.Should().Be(At(Tuesday, 10));
        result.BreachAt.Should().BeNull();
    }

    [Fact]
    public void TC08_WeekendPauseOnlyCountsWorkingMinutes()
    {
        var result = Calculate(
            At(Friday, 9), 480, BaseCalendar(),
            new StatusEvent(At(Friday, 16), StatusKind.Pause),
            new StatusEvent(At(NextMonday, 10), StatusKind.Active));

// 8 рабочих часов от Пт 09:00 → Пт 17:00.
        // Пауза Пт 16:00 – Пн 10:00 = 2 ч (Пт) + 1 ч (Пн) = 3 рабочих часа → Пн 11:00.
        result.Deadline.Should().Be(At(NextMonday, 11));
    }

    [Fact]
    public void TC09_MultiDayPauseCountsOnlyWorkingTime()
    {
        var result = Calculate(
            At(Monday, 9), 1920, BaseCalendar(),
            new StatusEvent(At(Monday, 15), StatusKind.Pause),
            new StatusEvent(At(Wednesday, 12), StatusKind.Active));

// 32 рабочих часа от Пн 09:00 = Чт 14:00 (9 + 9 + 9 + 5).
        // Пауза Пн 15:00 – Ср 12:00 = 3 ч + 9 ч + 3 ч = 15 рабочих часов → Пн 11:00 следующей недели.
        result.Deadline.Should().Be(At(NextMonday, 11));
    }

    [Fact]
    public void TC10_PauseAfterDeadlineDoesNotShiftTimer()
    {
        var result = Calculate(
            At(Friday, 17), 240, BaseCalendar(),
            new StatusEvent(At(Friday, 18), StatusKind.Pause),
            new StatusEvent(At(NextMonday, 9), StatusKind.Active));

        // 4 рабочих часа от Пт 17:00: 1 час в пятницу + 3 часа в понедельник = Пн 12:00.
        result.Deadline.Should().Be(At(NextMonday, 12));
    }

    [Fact]
    public void TC11_WorkingDayExceptionWinsOverHoliday()
    {
        var calendar = SlaCalendar.BusinessWeek(
            SlaCalendar.DefaultTimeZoneId, Day,
            holidays: [Monday],
            exceptions: [CalendarException.Working(Monday, Day)]);

// Исключение «рабочий день» перекрывает праздник, поэтому SLA идёт с Пн 09:00 → Пн 11:00.
        SlaCalculator.AddWorkingMinutes(At(Monday, 9), 120, calendar).Should().Be(At(Monday, 11));
    }

    [Fact]
    public void TC12_ShortenedDayRollsOverToNextWorkingDay()
    {
        var shortFriday = new WorkingInterval(new TimeOnly(9, 0), new TimeOnly(15, 0));
        var calendar = SlaCalendar.BusinessWeek(
            SlaCalendar.DefaultTimeZoneId, Day,
            exceptions: [CalendarException.Working(Friday, shortFriday)]);

        SlaCalculator.AddWorkingMinutes(At(Friday, 14), 120, calendar).Should().Be(At(NextMonday, 10));
    }

    [Fact]
    public void TC13_LunchBreakIsNotWorkingTime()
    {
        var weekly = Enum.GetValues<DayOfWeek>().ToDictionary(
            day => day,
            day => day is DayOfWeek.Saturday or DayOfWeek.Sunday
                ? (IReadOnlyList<WorkingInterval>)Array.Empty<WorkingInterval>()
                : new[]
                {
                    new WorkingInterval(new TimeOnly(9, 0), new TimeOnly(13, 0)),
                    new WorkingInterval(new TimeOnly(14, 0), new TimeOnly(18, 0))
                });

        var calendar = new SlaCalendar(
            SlaCalendar.DefaultTimeZoneId, weekly,
            new HashSet<DateOnly>(),
            new Dictionary<DateOnly, CalendarException>());

        SlaCalculator.AddWorkingMinutes(At(Monday, 12), 120, calendar).Should().Be(At(Monday, 15));
    }

    [Fact]
    public void TC14_NonPositiveDurationIsRejected()
    {
        var act = () => Calculate(At(Monday, 9), 0, BaseCalendar());

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TC15_PauseAfterBreachDoesNotCancelBreach()
    {
        var result = Calculate(
            At(Monday, 9), 120, BaseCalendar(),
            new StatusEvent(At(Monday, 12), StatusKind.Pause),
            new StatusEvent(At(Monday, 14), StatusKind.Active));

        result.BreachAt.Should().Be(At(Monday, 11));
        result.Deadline.Should().Be(At(Monday, 11));
    }

    [Fact]
    public void PauseEntirelyOutsideWorkingHoursProducesNoShift()
    {
        var withoutPause = Calculate(At(Monday, 9), 120, BaseCalendar());
        var withPause = Calculate(
            At(Monday, 9), 120, BaseCalendar(),
            new StatusEvent(At(Monday, 19), StatusKind.Pause),
            new StatusEvent(At(Tuesday, 8), StatusKind.Active));

        withPause.Deadline.Should().Be(withoutPause.Deadline);
    }

    [Fact]
    public void PauseIsCountedOnceWhenStatusRepeats()
    {
        var result = Calculate(
            At(Monday, 9), 120, BaseCalendar(),
            new StatusEvent(At(Monday, 10), StatusKind.Pause),
            new StatusEvent(At(Monday, 11), StatusKind.Pause),
            new StatusEvent(At(Monday, 12), StatusKind.Active));

result.BreachAt.Should().BeNull();
        result.Deadline.Should().Be(At(Monday, 13));
    }

    [Fact]
    public void SecondPauseAfterDeadlineDoesNotCancelBreach()
    {
        var result = Calculate(
            At(Monday, 9), 120, BaseCalendar(),
            new StatusEvent(At(Monday, 10), StatusKind.Pause),
            new StatusEvent(At(Monday, 11), StatusKind.Active),
new StatusEvent(At(Monday, 12), StatusKind.Pause),
            new StatusEvent(At(Monday, 13), StatusKind.Active));

        // Первая пауза сдвигает дедлайн с 11:00 до 12:00; вторая пауза в 12:00 фиксирует просрочку и не отменяет её.
        result.BreachAt.Should().Be(At(Monday, 12));
        result.Deadline.Should().Be(At(Monday, 12));
    }

    [Fact]
    public void ClosedWhilePausedStopsTimerAndKeepsBreachState()
    {
        var result = Calculate(
            At(Monday, 9), 480, BaseCalendar(),
            new StatusEvent(At(Monday, 10), StatusKind.Pause),
            new StatusEvent(At(Monday, 12), StatusKind.Stop));

        result.IsPaused.Should().BeFalse();
        result.BreachAt.Should().BeNull();
    }

    [Fact]
    public void BreachAfterDeadlineIsReportedOnEvaluation()
    {
        var createdAt = At(Monday, 9);
        var result = SlaCalculator.CalculateDeadline(
            createdAt, 120, BaseCalendar(), [],
            evaluatedAt: At(Monday, 13));

        result.BreachAt.Should().Be(At(Monday, 11));
        result.IsBreachedAt(At(Monday, 13)).Should().BeTrue();
    }

    [Fact]
    public void WorkingMinutesBetweenCountsOnlyOverlappingIntervals() =>
        SlaCalculator.WorkingMinutesBetween(At(Monday, 16), At(Tuesday, 10), BaseCalendar()).Should().Be(180);

    [Fact]
    public void WorkingMinutesBetweenReturnsZeroForReversedRange() =>
        SlaCalculator.WorkingMinutesBetween(At(Monday, 10), At(Monday, 9), BaseCalendar()).Should().Be(0);

    [Fact]
    public void Always24x7CalendarHasNoGaps()
    {
        var start = At(Monday, 22);

        SlaCalculator.AddWorkingMinutes(start, 240, SlaCalendar.Always24x7())
            .Should().BeCloseTo(start.AddHours(4), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void StatusEventsAreProcessedInChronologicalOrder()
    {
        var ordered = Calculate(
            At(Monday, 9), 120, BaseCalendar(),
            new StatusEvent(At(Monday, 11), StatusKind.Active),
            new StatusEvent(At(Monday, 10), StatusKind.Pause));

        var same = Calculate(
            At(Monday, 9), 120, BaseCalendar(),
            new StatusEvent(At(Monday, 10), StatusKind.Pause),
            new StatusEvent(At(Monday, 11), StatusKind.Active));

        ordered.Deadline.Should().Be(same.Deadline);
    }
}

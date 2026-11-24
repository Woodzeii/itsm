using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence.Services;

namespace itsm.Tests.UnitTests.Infrastructure;

public class SlaCalendarCalculatorTests
{
    [Theory]
    [InlineData("2025-01-06T10:00:00+03:00", 120, "2025-01-06T12:00:00+03:00")]
    [InlineData("2025-01-06T17:00:00+03:00", 120, "2025-01-07T10:00:00+03:00")]
    [InlineData("2025-01-06T17:55:00+03:00", 120, "2025-01-07T10:55:00+03:00")]
    [InlineData("2025-01-06T18:00:00+03:00", 120, "2025-01-07T11:00:00+03:00")]
    [InlineData("2025-01-11T12:00:00+03:00", 120, "2025-01-13T11:00:00+03:00")]
    public void AddsMinutesAcrossBusinessBoundaries(string start, int minutes, string expected)
    {
        var result = SlaCalendarCalculator.AddWorkingMinutes(
            DateTimeOffset.Parse(start),
            minutes,
            BaseCalendar());

        Assert.Equal(DateTimeOffset.Parse(expected), result);
    }

    [Fact]
    public void HolidayExcludesDateAndWorkingExceptionOverridesIt()
    {
        var holiday = BaseCalendar(
            new SlaCalendarDateOverride(new DateOnly(2025, 1, 13), false, null));
        var workedHoliday = BaseCalendar(
            new SlaCalendarDateOverride(
                new DateOnly(2025, 1, 13),
                true,
                [new SlaWorkingInterval(540, 1080)]));

        var afterHoliday = SlaCalendarCalculator.AddWorkingMinutes(
            DateTimeOffset.Parse("2025-01-10T17:00:00+03:00"),
            120,
            holiday);
        var afterOverride = SlaCalendarCalculator.AddWorkingMinutes(
            DateTimeOffset.Parse("2025-01-10T17:00:00+03:00"),
            120,
            workedHoliday);

        Assert.Equal(DateTimeOffset.Parse("2025-01-14T10:00:00+03:00"), afterHoliday);
        Assert.Equal(DateTimeOffset.Parse("2025-01-13T10:00:00+03:00"), afterOverride);
    }

    [Fact]
    public void UsesShortenedDayAndSplitIntervals()
    {
        var shortFriday = BaseCalendar(
            new SlaCalendarDateOverride(
                new DateOnly(2025, 1, 10),
                true,
                [new SlaWorkingInterval(540, 900)]));
        var splitDay = BaseCalendar(
            weekDayOverrides:
            [
                new SlaCalendarWeekDay(1, [new SlaWorkingInterval(540, 780), new SlaWorkingInterval(840, 1080)])
            ]);

        var afterShortDay = SlaCalendarCalculator.AddWorkingMinutes(
            DateTimeOffset.Parse("2025-01-10T14:00:00+03:00"),
            120,
            shortFriday);
        var afterLunch = SlaCalendarCalculator.AddWorkingMinutes(
            DateTimeOffset.Parse("2025-01-06T12:00:00+03:00"),
            120,
            splitDay);

        Assert.Equal(DateTimeOffset.Parse("2025-01-13T10:00:00+03:00"), afterShortDay);
        Assert.Equal(DateTimeOffset.Parse("2025-01-06T15:00:00+03:00"), afterLunch);
    }

    [Theory]
    [InlineData("2025-01-06T17:00:00+03:00", "2025-01-06T10:00:00+03:00", "2025-01-06T12:00:00+03:00", "2025-01-07T10:00:00+03:00")]
    [InlineData("2025-01-10T17:00:00+03:00", "2025-01-10T16:00:00+03:00", "2025-01-13T10:00:00+03:00", "2025-01-13T11:00:00+03:00")]
    [InlineData("2025-01-09T14:00:00+03:00", "2025-01-07T09:00:00+03:00", "2025-01-10T09:00:00+03:00", "2025-01-14T14:00:00+03:00")]
    public void PauseShiftsDeadlineOnlyByWorkingTime(
        string deadlineValue,
        string pauseStart,
        string pauseEnd,
        string expected)
    {
        var calendar = BaseCalendar();
        var deadline = DateTimeOffset.Parse(deadlineValue);
        var pausedWorkingTime = SlaCalendarCalculator.WorkingTimeBetween(
            DateTimeOffset.Parse(pauseStart),
            DateTimeOffset.Parse(pauseEnd),
            calendar);

        var shiftedDeadline = SlaCalendarCalculator.AddWorkingTime(deadline, pausedWorkingTime, calendar);

        Assert.Equal(DateTimeOffset.Parse(expected), shiftedDeadline);
    }

    [Fact]
    public void PauseOutsideWorkingHoursDoesNotShiftDeadline()
    {
        var calendar = BaseCalendar();
        var pause = SlaCalendarCalculator.WorkingTimeBetween(
            DateTimeOffset.Parse("2025-01-10T18:00:00+03:00"),
            DateTimeOffset.Parse("2025-01-13T09:00:00+03:00"),
            calendar);

        Assert.Equal(TimeSpan.Zero, pause);
    }

    [Fact]
    public void CalculatesAbsoluteMinutesAcrossDaylightSavingTransition()
    {
        var calendar = new SlaCalendarSnapshot(
            "Europe/Berlin",
            "work_hours",
            1,
            [new SlaCalendarWeekDay(0, [new SlaWorkingInterval(60, 240)])],
            []);

        var workTime = SlaCalendarCalculator.WorkingTimeBetween(
            DateTimeOffset.Parse("2025-03-30T01:00:00+01:00"),
            DateTimeOffset.Parse("2025-03-30T04:00:00+02:00"),
            calendar);

        Assert.Equal(TimeSpan.FromHours(2), workTime);
    }

    private static SlaCalendarSnapshot BaseCalendar(
        params SlaCalendarDateOverride[] exceptions) =>
        BaseCalendar(Array.Empty<SlaCalendarWeekDay>(), exceptions);

    private static SlaCalendarSnapshot BaseCalendar(
        IReadOnlyList<SlaCalendarWeekDay> weekDayOverrides,
        params SlaCalendarDateOverride[] exceptions)
    {
        var weekdays = Enumerable.Range(1, 5)
            .Select(day => new SlaCalendarWeekDay(day, [new SlaWorkingInterval(540, 1080)]))
            .ToArray();
        foreach (var overrideDay in weekDayOverrides)
            weekdays[overrideDay.DayOfWeek - 1] = overrideDay;

        return new SlaCalendarSnapshot("Europe/Moscow", "work_hours", 1, weekdays, exceptions);
    }
}

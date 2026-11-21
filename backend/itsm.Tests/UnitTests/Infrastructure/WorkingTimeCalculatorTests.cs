using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence.Services;

namespace itsm.Tests.UnitTests.Infrastructure;

public sealed class WorkingTimeCalculatorTests
{
    [Fact]
    public void AddWorkingMinutesCarriesRemainingTimeToNextWorkingDay()
    {
        var deadline = WorkingTimeCalculator.AddWorkingMinutes(
            Moscow(2025, 1, 6, 17, 0),
            120,
            BaseCalendar());

        Assert.Equal(Moscow(2025, 1, 7, 10, 0), deadline);
    }

    [Fact]
    public void AddWorkingMinutesSkipsHolidayAndHonorsMultipleDailyIntervals()
    {
        var calendar = BaseCalendar();
        calendar = calendar with
        {
            Exceptions = new Dictionary<DateOnly, CalendarDateOverride>
            {
                [new DateOnly(2025, 1, 6)] = new(new DateOnly(2025, 1, 6), false, null, null)
            }
        };
        var holidayDeadline = WorkingTimeCalculator.AddWorkingMinutes(
            Moscow(2025, 1, 3, 17, 0),
            120,
            calendar);

        var splitDayCalendar = calendar with
        {
            Exceptions = new Dictionary<DateOnly, CalendarDateOverride>(),
            Weekly = new Dictionary<int, WorkingIntervalSnapshot[]>
            {
                [1] =
                [
                    new(new TimeOnly(9, 0), new TimeOnly(13, 0)),
                    new(new TimeOnly(14, 0), new TimeOnly(18, 0))
                ]
            }
        };
        var lunchDeadline = WorkingTimeCalculator.AddWorkingMinutes(
            Moscow(2025, 1, 6, 12, 0),
            120,
            splitDayCalendar);

        Assert.Equal(Moscow(2025, 1, 7, 10, 0), holidayDeadline);
        Assert.Equal(Moscow(2025, 1, 6, 15, 0), lunchDeadline);
    }

    [Fact]
    public void WorkingMinutesBetweenCountsOnlyWorkingTimeAcrossWeekend()
    {
        var minutes = WorkingTimeCalculator.WorkingMinutesBetween(
            Moscow(2025, 1, 10, 16, 0),
            Moscow(2025, 1, 13, 10, 0),
            BaseCalendar());

        Assert.Equal(180, minutes);
    }

    [Fact]
    public void CalendarExceptionOverridesWeeklySchedule()
    {
        var schedule = new WorkingSchedule
        {
            ScheduleType = "work_hours",
            TimeZoneId = "Europe/Moscow",
            Version = 3
        };
        var hours = new[]
        {
            new WorkingHour
            {
                DayOfWeek = 1,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(18, 0)
            }
        };
        var exceptions = new[]
        {
            new CalendarException
            {
                ExceptionDate = new DateOnly(2025, 1, 6),
                IsWorkDay = true,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(15, 0)
            }
        };
        var snapshot = WorkingTimeCalculator.CreateSnapshot(schedule, hours, exceptions);

        var deadline = WorkingTimeCalculator.AddWorkingMinutes(
            Moscow(2025, 1, 6, 14, 0),
            120,
            snapshot);

        Assert.Equal(Moscow(2025, 1, 7, 10, 0), deadline);
    }

    [Fact]
    public void CalendarSnapshotRoundTripsThroughJson()
    {
        var snapshot = BaseCalendar() with
        {
            Exceptions = new Dictionary<DateOnly, CalendarDateOverride>
            {
                [new DateOnly(2025, 1, 6)] = new(new DateOnly(2025, 1, 6), false, null, null)
            }
        };
        var restored = WorkingTimeCalculator.Deserialize(WorkingTimeCalculator.Serialize(snapshot));

        Assert.Equal(
            WorkingTimeCalculator.AddWorkingMinutes(Moscow(2025, 1, 3, 17, 0), 120, snapshot),
            WorkingTimeCalculator.AddWorkingMinutes(Moscow(2025, 1, 3, 17, 0), 120, restored));
        Assert.Equal(snapshot.Version, restored.Version);
    }

    [Fact]
    public void AddWorkingMinutesUsesAbsoluteTimeAcrossDaylightSavingTransition()
    {
        var calendar = new WorkingCalendarSnapshot(
            1,
            "Europe/Berlin",
            false,
            new Dictionary<int, WorkingIntervalSnapshot[]>
            {
                [0] = [new(new TimeOnly(1, 30), new TimeOnly(3, 30))]
            },
            new Dictionary<DateOnly, CalendarDateOverride>());
        var start = new DateTimeOffset(2025, 3, 30, 1, 30, 0, TimeSpan.FromHours(1));

        var deadline = WorkingTimeCalculator.AddWorkingMinutes(start, 60, calendar);

        Assert.Equal(
            new DateTimeOffset(2025, 3, 30, 3, 30, 0, TimeSpan.FromHours(2)),
            deadline);
    }

    private static WorkingCalendarSnapshot BaseCalendar() =>
        new(
            1,
            "Europe/Moscow",
            false,
            Enumerable.Range(1, 5).ToDictionary(
                day => day,
                _ => new[] { new WorkingIntervalSnapshot(new TimeOnly(9, 0), new TimeOnly(18, 0)) }),
            new Dictionary<DateOnly, CalendarDateOverride>());

    private static DateTimeOffset Moscow(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.FromHours(3));
}

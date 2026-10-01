namespace itsm.Domain.Entities;
public class WorkingHour
{
    public int Id { get; set; }
    public int WorkingScheduleId { get; set; }
    public int DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public WorkingSchedule WorkingSchedule { get; set; } = null!;
}
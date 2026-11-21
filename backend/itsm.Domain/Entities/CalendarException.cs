namespace itsm.Domain.Entities;

public class CalendarException
{
    public int Id { get; set; }
    public int? WorkingScheduleId { get; set; }
    public DateOnly ExceptionDate { get; set; }
    public bool IsWorkDay { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Description { get; set; }
}
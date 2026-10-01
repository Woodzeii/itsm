namespace itsm.Domain.Entities;
public class WorkingSchedule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ScheduleType { get; set; } = "24x7";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<WorkingHour> Hours { get; set; } = new List<WorkingHour>();
}
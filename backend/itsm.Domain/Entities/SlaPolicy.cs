namespace itsm.Domain.Entities;

public class SlaPolicy
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? TicketTypeId { get; set; }
    public int? CriticalityLevelId { get; set; }
    public int? WorkingScheduleId { get; set; }
    public string? ScheduleType { get; set; }
    public int ReactionTimeMinutes { get; set; }
    public int ResolutionTimeMinutes { get; set; }
}
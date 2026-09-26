namespace itsm.Domain.Entities;

public class SlaPolicy
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ScheduleType { get; set; }
    public int ReactionTimeMinutes { get; set; }
    public int ResolutionTimeMinutes { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
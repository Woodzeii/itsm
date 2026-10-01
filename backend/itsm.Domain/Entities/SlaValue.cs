namespace itsm.Domain.Entities;
public class SlaValue
{
    public int Id { get; set; }
    public int TicketTypeId { get; set; }
    public int CriticalityLevelId { get; set; }
    public int? ReactionTimeMinutes { get; set; }
    public int? ResolutionTimeMinutes { get; set; }
    public bool IsDisabled { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public TicketType TicketType { get; set; } = null!;
    public CriticalityLevel CriticalityLevel { get; set; } = null!;
}
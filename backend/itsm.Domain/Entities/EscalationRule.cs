namespace itsm.Domain.Entities;
public class EscalationRule
{
    public int Id { get; set; }
    public int TicketTypeId { get; set; }
    public bool OnSlaBreach { get; set; } = true;
    public bool OnManual { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public TicketType TicketType { get; set; } = null!;
}
namespace itsm.Domain.Entities;
public class TicketStatusTransition
{
    public int Id { get; set; }
    public int FromStatusId { get; set; }
    public int ToStatusId { get; set; }
    public string? RequiredRoleCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public TicketStatus FromStatus { get; set; } = null!;
    public TicketStatus ToStatus { get; set; } = null!;
}
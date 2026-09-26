namespace itsm.Domain.Entities;

public class TicketApproval
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int ApproverId { get; set; }
    public int StepNumber { get; set; }
    public string? Status { get; set; } = "Pending";
    public string? Resolution { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public User Approver { get; set; } = null!;
}
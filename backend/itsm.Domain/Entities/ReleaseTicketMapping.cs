namespace itsm.Domain.Entities;

public class ReleaseTicketMapping
{
    public int ReleaseTicketId { get; set; }
    public int TaskTicketId { get; set; }

    public Ticket ReleaseTicket { get; set; } = null!;
    public Ticket TaskTicket { get; set; } = null!;
}
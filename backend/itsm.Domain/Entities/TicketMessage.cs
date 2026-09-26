namespace itsm.Domain.Entities;

public class TicketMessage
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int AuthorId { get; set; }
    public string MessageBody { get; set; } = string.Empty;
    public bool? IsInternal { get; set; } = false;
    public string? SourceType { get; set; } = "web";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public User Author { get; set; } = null!;
}
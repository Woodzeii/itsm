namespace itsm.Domain.Entities;
public class TicketEscalation
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int? InitiatorUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int? FromCriticalityLevelId { get; set; }
    public int? ToCriticalityLevelId { get; set; }
    public string? NotifiedUserIds { get; set; }
    public DateTimeOffset EscalatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public User? Initiator { get; set; }
    public CriticalityLevel? FromCriticalityLevel { get; set; }
    public CriticalityLevel? ToCriticalityLevel { get; set; }
}
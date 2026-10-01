namespace itsm.Domain.Entities;
public class Attachment
{
    public int Id { get; set; }
    public int TicketMessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int UploadedById { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    public TicketMessage TicketMessage { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
}
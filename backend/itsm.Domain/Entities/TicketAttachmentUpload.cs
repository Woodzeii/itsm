namespace itsm.Domain.Entities;

public class TicketAttachmentUpload
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TicketId { get; set; }
    public int UploadedById { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
}

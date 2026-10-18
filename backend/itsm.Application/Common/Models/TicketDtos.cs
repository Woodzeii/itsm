namespace itsm.Application.Common.Models;

// ==================== TicketDto ====================

public class TicketDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TypeId { get; set; }
    public string? TypeName { get; set; }
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public int? CriticalityLevelId { get; set; }
    public string? CriticalityLevelName { get; set; }
    public int CreatorId { get; set; }
    public string? CreatorName { get; set; }
    public int? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public int? AssetId { get; set; }
    public string? AssetName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public int MessageCount { get; set; }
}

// ==================== Requests ====================

public class CreateTicketRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TypeId { get; set; }
    public int? CriticalityLevelId { get; set; }
    public int? AssetId { get; set; }
    public Dictionary<string, object?>? CustomFieldsData { get; set; }
}

public class UpdateTicketRequest
{
    public int? CriticalityLevelId { get; set; }
    public int? AssigneeId { get; set; }
    public Dictionary<string, object?>? CustomFieldsData { get; set; }
}

public class ChangeTicketStatusRequest
{
    public int StatusId { get; set; }
    public string? RepairResolution { get; set; }
}

public class EscalateTicketRequest
{
    public string? Reason { get; set; }
}

// ==================== TicketEscalationDto ====================

public class TicketEscalationDto
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int? InitiatorUserId { get; set; }
    public string? InitiatorName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int? FromCriticalityLevelId { get; set; }
    public string? FromCriticalityLevelName { get; set; }
    public int? ToCriticalityLevelId { get; set; }
    public string? ToCriticalityLevelName { get; set; }
    public DateTimeOffset EscalatedAt { get; set; }
}

// ==================== TicketMessageDto ====================

public class TicketMessageDto
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string MessageBody { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<AttachmentDto> Attachments { get; set; } = new();
}

public class AttachmentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}

public class CreateTicketMessageRequest
{
    public string MessageBody { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public List<string>? Attachments { get; set; }
}

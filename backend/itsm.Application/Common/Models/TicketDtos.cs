using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace itsm.Application.Common.Models;

/// <summary>Request payload for creating a ticket.</summary>
public sealed record CreateTicketRequest
{
    [Required, StringLength(300, MinimumLength = 1)]
    public required string Title { get; init; }

    [Required, StringLength(10000, MinimumLength = 1)]
    public required string Description { get; init; }

    [Range(1, int.MaxValue)]
    public required int TypeId { get; init; }

    [Required, StringLength(50, MinimumLength = 1)]
    public required string Priority { get; init; }

    public int? AssetId { get; init; }

    public JsonElement? CustomFieldsData { get; init; }
}

/// <summary>Request payload for updating a ticket.</summary>
public sealed record UpdateTicketRequest
{
    [StringLength(50, MinimumLength = 1)]
    public string? Priority { get; init; }

    public JsonElement AssigneeId { get; init; }

    public JsonElement? CustomFieldsData { get; init; }
}

/// <summary>Request payload for changing a ticket status.</summary>
public sealed record ChangeTicketStatusRequest
{
    [Range(1, int.MaxValue)]
    public required int StatusId { get; init; }

    [StringLength(10000)]
    public string? RepairResolution { get; init; }

    public RepairOutcome? RepairOutcome { get; init; }
}

/// <summary>Possible asset lifecycle outcomes after completing a repair ticket.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RepairOutcome>))]
public enum RepairOutcome
{
    ReturnToExploitation,
    TransferToWarehouse,
    Retire
}

internal sealed class RepairOutcomeJsonConverter : JsonStringEnumConverter<RepairOutcome>
{
    public RepairOutcomeJsonConverter() : base(allowIntegerValues: false)
    {
    }
}

/// <summary>Request payload for assigning a ticket to an engineer.</summary>
public sealed record AssignTicketRequest
{
    [Range(1, int.MaxValue)]
    public required int AssigneeId { get; init; }
}

/// <summary>Request payload for adding a message to a ticket.</summary>
public sealed record CreateTicketMessageRequest
{
    [Required, StringLength(10000, MinimumLength = 1)]
    public required string MessageBody { get; init; }

    public bool IsInternal { get; init; }

    public IReadOnlyList<string>? Attachments { get; init; }
}

/// <summary>Ticket details returned by the API.</summary>
public sealed record TicketResponse(
    int Id,
    string Title,
    string? Description,
    int TypeId,
    string TypeName,
    int StatusId,
    string StatusName,
    string? Priority,
    int? CriticalityLevelId,
    int? ServiceId,
    int CreatorId,
    string CreatorName,
    int? AssigneeId,
    string? AssigneeName,
    int? AssigneeGroupId,
    int? AssetId,
    JsonElement? CustomFieldsData,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SlaReactionDeadline,
    DateTimeOffset? SlaResolutionDeadline);

/// <summary>A message and its author returned for a ticket.</summary>
public sealed record TicketMessageResponse(
    int Id,
    int TicketId,
    int AuthorId,
    string AuthorName,
    string MessageBody,
    bool IsInternal,
    string? SourceType,
    IReadOnlyList<TicketAttachmentResponse> Attachments,
    DateTimeOffset CreatedAt);

/// <summary>Attachment metadata returned for a ticket message.</summary>
public sealed record TicketAttachmentResponse(
    int Id,
    string FileName);

/// <summary>Downloaded ticket attachment content and its safe download name.</summary>
public sealed record TicketAttachmentFile(
    Stream Content,
    string FileName);

/// <summary>Details needed to associate an uploaded file with a ticket message.</summary>
public sealed record TicketAttachmentUploadResponse(
    string Id,
    string FileName,
    long SizeBytes,
    DateTimeOffset ExpiresAt);

/// <summary>Configured service desk head returned by the API.</summary>
public sealed record ServiceDeskHeadSettingResponse(
    int? UserId,
    string? Login,
    string? FullName);

/// <summary>Request payload for selecting the service desk head.</summary>
public sealed record SetServiceDeskHeadRequest
{
    [Range(1, int.MaxValue)]
    public required int UserId { get; init; }
}

/// <summary>Escalation policy for a ticket type.</summary>
public sealed record EscalationRuleResponse(
    int TicketTypeId,
    bool OnSlaBreach,
    bool OnManual,
    bool IsActive);

/// <summary>Request payload for changing a ticket type's escalation policy.</summary>
public sealed record UpdateEscalationRuleRequest
{
    public required bool OnSlaBreach { get; init; }
    public required bool OnManual { get; init; }
    public required bool IsActive { get; init; }
}

/// <summary>Ticket summary returned in an asset's ticket history.</summary>
public sealed record AssetTicketHistoryItem(
    int Id,
    int TypeId,
    string TypeName,
    string Title,
    int CreatorId,
    string CreatorName,
    int? AssigneeId,
    string? AssigneeName,
    int StatusId,
    string StatusName,
    int? CriticalityLevelId,
    string? CriticalityName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt);

/// <summary>Details recorded for a manual ticket escalation.</summary>
public sealed record TicketEscalationResponse(
    int Id,
    int TicketId,
    int? InitiatorUserId,
    string Reason,
    int? FromCriticalityLevelId,
    int? ToCriticalityLevelId,
    IReadOnlyList<int> NotifiedUserIds,
    DateTimeOffset EscalatedAt);

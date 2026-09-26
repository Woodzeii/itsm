using System.Text.Json;

namespace itsm.Domain.Entities;

public class Ticket
{
	public int Id { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public int TypeId { get; set; }
	public int StatusId { get; set; }
	public string? Priority { get; set; }
	public int? ServiceId { get; set; }
	public int CreatorId { get; set; }
	public int? AssigneeId { get; set; }
	public int? AssigneeGroupId { get; set; }
	public JsonElement? CustomFieldsData { get; set; }
	public int? SlaPolicyId { get; set; }
	public DateTimeOffset? SlaReactionDeadline { get; set; }
	public DateTimeOffset? SlaResolutionDeadline { get; set; }
	public bool? IsReactionEscalated { get; set; }
	public bool? IsResolutionEscalated { get; set; }
	public DateTimeOffset? ActualReactionTime { get; set; }
	public DateTimeOffset? ActualResolutionTime { get; set; }
	public bool? SlaTimerPaused { get; set; }
	public int? SlaAccumulatedPauseMinutes { get; set; }
	public string? TargetSystem { get; set; }
	public DateTimeOffset? PlannedStart { get; set; }
	public DateTimeOffset? PlannedEnd { get; set; }
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

	public TicketType Type { get; set; } = null!;
	public TicketStatus Status { get; set; } = null!;
	public ServiceCatalog? Service { get; set; }
	public User Creator { get; set; } = null!;
	public User? Assignee { get; set; }
	public AgentGroup? AssigneeGroup { get; set; }
	public SlaPolicy? SlaPolicy { get; set; }
	public ICollection<TicketAssetMapping> AssetMappings { get; set; } = new List<TicketAssetMapping>();
	public ICollection<ReleaseTicketMapping> ReleaseMappings { get; set; } = new List<ReleaseTicketMapping>();
	public ICollection<ReleaseTicketMapping> IncludedInReleases { get; set; } = new List<ReleaseTicketMapping>();
	public ICollection<TicketApproval> Approvals { get; set; } = new List<TicketApproval>();
	public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
	public ICollection<TicketAuditLog> AuditLogs { get; set; } = new List<TicketAuditLog>();
}

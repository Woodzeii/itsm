namespace itsm.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string? ObjectSid { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public int? ManagerId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? TwoFaSecret { get; set; }
    public string Status { get; set; } = "Unverified";
    public string? PasswordHash { get; set; }
    public string? VerificationTokenHash { get; set; }
    public DateTimeOffset? VerificationTokenExpiresAt { get; set; }
    public int? TenantId { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? PasswordChangedAt { get; set; }
    public bool IsManager { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? Manager { get; set; }
    public Tenant? Tenant { get; set; }
    public ICollection<User> DirectReports { get; set; } = new List<User>();
    public ICollection<UserRoleMapping> RoleMappings { get; set; } = new List<UserRoleMapping>();
    public ICollection<Ticket> CreatedTickets { get; set; } = new List<Ticket>();
    public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
    public ICollection<TicketApproval> Approvals { get; set; } = new List<TicketApproval>();
    public ICollection<Asset> AssignedAssets { get; set; } = new List<Asset>();
    public ICollection<AssetHistoryLog> AssetHistoryActions { get; set; } = new List<AssetHistoryLog>();
    public ICollection<AssetHistoryLog> PreviousAssetAssignments { get; set; } = new List<AssetHistoryLog>();
    public ICollection<AssetHistoryLog> NewAssetAssignments { get; set; } = new List<AssetHistoryLog>();
    public ICollection<TicketMessage> TicketMessages { get; set; } = new List<TicketMessage>();
    public ICollection<TicketAuditLog> TicketAuditLogs { get; set; } = new List<TicketAuditLog>();
    public ICollection<LoginAudit> LoginAudits { get; set; } = new List<LoginAudit>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
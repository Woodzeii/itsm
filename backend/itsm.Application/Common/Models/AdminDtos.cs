namespace itsm.Application.Common.Models;

// ==================== Audit Log ====================
/// <summary>
/// ⚠️ НЕ РЕАЛИЗОВАНО В BACKEND. Используется как хардкод-мок для фронта.
/// </summary>
public class AuditLogEntryDto
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string? UserFullName { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

// ==================== Role Summary ====================
public class RoleSummaryDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
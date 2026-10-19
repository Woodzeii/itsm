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

// ==================== User Summary ====================
/// <summary>
/// ⚠️ НЕ РЕАЛИЗОВАНО В BACKEND. Мок для фронта.
/// </summary>
public class UserSummaryDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public int? ManagerId { get; set; }
    public List<int> RoleIds { get; set; } = new();
    public bool IsActive { get; set; }
}

// ==================== Role Summary ====================
/// <summary>
/// ⚠️ НЕ РЕАЛИЗОВАНО В BACKEND. Мок для фронта.
/// </summary>
public class RoleSummaryDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
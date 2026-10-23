namespace itsm.Application.Common.Models;

public class UserAdminDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public int? ManagerId { get; set; }
    public int? TenantId { get; set; }
    public bool IsActive { get; set; }
    public bool IsManager { get; set; }
    public bool IsTwoFactorEnabled { get; set; }
    public string Status { get; set; } = string.Empty;
    public int FailedLoginAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<int> RoleIds { get; set; } = new();
    public List<string> RoleCodes { get; set; } = new();
}

public class UserListQuery
{
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public int? RoleId { get; set; }
    public int? TenantId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Department { get; set; }
    public int? ManagerId { get; set; }
    public int? TenantId { get; set; }
    public bool IsManager { get; set; }
    public List<int> RoleIds { get; set; } = new();
}

public class UpdateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Department { get; set; }
    public int? ManagerId { get; set; }
    public int? TenantId { get; set; }
}

public class SetUserActiveRequest
{
    public bool IsActive { get; set; }
}

public class SetUserRolesRequest
{
    public List<int> RoleIds { get; set; } = new();
    public List<string> RoleCodes { get; set; } = new();
    public bool IsManager { get; set; }
}

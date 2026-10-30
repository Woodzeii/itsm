using System.Text.Json;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Persistence.Services;

public class UserAdminService : IUserAdminService
{
    private const string AdminRoleCode = "admin";

    private readonly ItsmDbContext _db;
    private readonly IPasswordHasher _hasher;

    public UserAdminService(ItsmDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<PagedResult<UserAdminDto>> GetListAsync(UserListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var q = _db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            q = q.Where(u => EF.Functions.ILike(u.Username, term)
                             || EF.Functions.ILike(u.Email, term)
                             || EF.Functions.ILike(u.FullName, term));
        }
        if (query.IsActive.HasValue)
            q = q.Where(u => u.IsActive == query.IsActive.Value);
        if (query.TenantId.HasValue)
            q = q.Where(u => u.TenantId == query.TenantId.Value);
        if (query.RoleId.HasValue)
            q = q.Where(u => u.RoleMappings.Any(m => m.RoleId == query.RoleId.Value));

        var total = await q.CountAsync(ct);
        var users = await q
            .Include(u => u.RoleMappings).ThenInclude(m => m.Role)
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<UserAdminDto>
        {
            Items = users.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<UserAdminDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking()
            .Include(u => u.RoleMappings).ThenInclude(m => m.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? null : ToDto(user);
    }

    public async Task<UserAdminDto> CreateAsync(CreateUserRequest request, int actorId, string? ip, CancellationToken ct = default)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim();

        if (await _db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower(), ct))
            throw new UserAdminException(UserAdminErrorKind.Conflict, "Пользователь с таким логином уже существует");
        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower(), ct))
            throw new UserAdminException(UserAdminErrorKind.Conflict, "Пользователь с таким email уже существует");

        await EnsureManagerExistsAsync(request.ManagerId, null, ct);
        await EnsureTenantExistsAsync(request.TenantId, ct);
        var roleIds = request.RoleIds.Distinct().ToList();
        await EnsureRolesExistAsync(roleIds, ct);

        var user = new User
        {
            Username = username,
            Email = email,
            FullName = request.FullName?.Trim() ?? string.Empty,
            Department = request.Department?.Trim(),
            ManagerId = request.ManagerId,
            TenantId = request.TenantId ?? 1,
            IsManager = request.IsManager,
            IsActive = true,
            Status = UserStatuses.Verified,
            PasswordHash = _hasher.Hash(request.Password),
            PasswordChangedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };
        foreach (var roleId in roleIds)
            user.RoleMappings.Add(new UserRoleMapping { RoleId = roleId });

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        AddAudit(actorId, user.Id, "user_created", null,
            new { user.Username, user.Email, RoleIds = roleIds, user.IsManager }, ip);
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(user.Id, ct))!;
    }

    public async Task<UserAdminDto> UpdateAsync(int id, UpdateUserRequest request, int actorId, string? ip, CancellationToken ct = default)
    {
        var user = await LoadAsync(id, ct);
        var email = request.Email.Trim();

        if (await _db.Users.AnyAsync(u => u.Id != id && u.Email.ToLower() == email.ToLower(), ct))
            throw new UserAdminException(UserAdminErrorKind.Conflict, "Пользователь с таким email уже существует");

        await EnsureManagerExistsAsync(request.ManagerId, id, ct);
        await EnsureTenantExistsAsync(request.TenantId, ct);

        var old = new { user.Email, user.FullName, user.Department, user.ManagerId, user.TenantId };

        user.Email = email;
        user.FullName = request.FullName?.Trim() ?? string.Empty;
        user.Department = request.Department?.Trim();
        user.ManagerId = request.ManagerId;
        user.TenantId = request.TenantId ?? 1;

        AddAudit(actorId, id, "user_updated", old,
            new { user.Email, user.FullName, user.Department, user.ManagerId, user.TenantId }, ip);
        await _db.SaveChangesAsync(ct);

        return ToDto(user);
    }

    public async Task DeleteAsync(int id, int actorId, string? ip, CancellationToken ct = default)
    {
        var user = await LoadAsync(id, ct);
        await EnsureCanDeactivateAsync(user, actorId, ct);

        var old = new { user.IsActive, user.Status };
        user.IsActive = false;
        AddAudit(actorId, id, "user_deleted", old, new { IsActive = false, Soft = true }, ip);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<UserAdminDto> SetActiveAsync(int id, bool isActive, int actorId, string? ip, CancellationToken ct = default)
    {
        var user = await LoadAsync(id, ct);
        if (!isActive)
            await EnsureCanDeactivateAsync(user, actorId, ct);

        var old = new { user.IsActive };
        user.IsActive = isActive;
        AddAudit(actorId, id, isActive ? "user_activated" : "user_deactivated", old, new { IsActive = isActive }, ip);
        await _db.SaveChangesAsync(ct);

        return ToDto(user);
    }

    public async Task<UserAdminDto> SetRolesAsync(int id, SetUserRolesRequest request, int actorId, string? ip, CancellationToken ct = default)
    {
        var user = await LoadAsync(id, ct);
        var newRoleIds = request.RoleIds.Distinct().ToList();
        var roleCodes = (request.RoleCodes ?? new()).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().ToList();
        if (newRoleIds.Count == 0 && roleCodes.Count > 0)
        {
            var found = await _db.SystemRoles
                .Where(r => r.Code != null && roleCodes.Contains(r.Code))
                .Select(r => r.Id).ToListAsync(ct);
            if (found.Count != roleCodes.Count)
                throw new UserAdminException(UserAdminErrorKind.Validation, "Указан несуществующий код роли");
            newRoleIds = found;
        }
        await EnsureRolesExistAsync(newRoleIds, ct);

        var adminRoleId = await _db.SystemRoles
            .Where(r => r.Code == AdminRoleCode).Select(r => (int?)r.Id).FirstOrDefaultAsync(ct);
        var wasAdmin = adminRoleId.HasValue && user.RoleMappings.Any(m => m.RoleId == adminRoleId.Value);
        var willBeAdmin = adminRoleId.HasValue && newRoleIds.Contains(adminRoleId.Value);

        if (wasAdmin && !willBeAdmin)
        {
            if (user.Id == actorId)
                throw new UserAdminException(UserAdminErrorKind.Conflict, "Нельзя снять с себя роль администратора");
            if (user.IsActive && await IsLastActiveAdminAsync(user.Id, ct))
                throw new UserAdminException(UserAdminErrorKind.Conflict, "Нельзя снять роль у последнего администратора");
        }

        var oldRoleIds = user.RoleMappings.Select(m => m.RoleId).OrderBy(x => x).ToList();
        var oldIsManager = user.IsManager;

        var toRemove = user.RoleMappings.Where(m => !newRoleIds.Contains(m.RoleId)).ToList();
        _db.UserRoleMappings.RemoveRange(toRemove);
        foreach (var m in toRemove) user.RoleMappings.Remove(m);
        foreach (var roleId in newRoleIds.Where(r => user.RoleMappings.All(m => m.RoleId != r)))
            user.RoleMappings.Add(new UserRoleMapping { UserId = user.Id, RoleId = roleId });
        user.IsManager = request.IsManager;

        AddAudit(actorId, id, "user_roles_changed",
            new { RoleIds = oldRoleIds, IsManager = oldIsManager },
            new { RoleIds = newRoleIds.OrderBy(x => x).ToList(), request.IsManager }, ip);
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<UserAdminDto> ResetMfaAsync(int id, int actorId, string? ip, CancellationToken ct = default)
    {
        var user = await LoadAsync(id, ct);

        var old = new { user.IsTwoFactorEnabled };
        user.IsTwoFactorEnabled = false;
        user.TwoFaSecret = null;
        user.TwoFactorEnabledAt = null;

        AddAudit(actorId, id, "user_mfa_reset", old, new { IsTwoFactorEnabled = false }, ip);
        await _db.SaveChangesAsync(ct);

        return ToDto(user);
    }

    public async Task<UserAdminDto> UnlockAsync(int id, int actorId, string? ip, CancellationToken ct = default)
    {
        var user = await LoadAsync(id, ct);

        var old = new { user.FailedLoginAttempts, user.LockedUntil };
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

        AddAudit(actorId, id, "user_unlocked", old, new { FailedLoginAttempts = 0, LockedUntil = (DateTimeOffset?)null }, ip);
        await _db.SaveChangesAsync(ct);

        return ToDto(user);
    }

    private async Task<User> LoadAsync(int id, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.RoleMappings).ThenInclude(m => m.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        return user ?? throw new UserAdminException(UserAdminErrorKind.NotFound, "Пользователь не найден");
    }

    private async Task EnsureCanDeactivateAsync(User user, int actorId, CancellationToken ct)
    {
        if (user.Id == actorId)
            throw new UserAdminException(UserAdminErrorKind.Conflict, "Нельзя деактивировать или удалить собственную учётную запись");
        if (user.IsActive && await IsLastActiveAdminAsync(user.Id, ct))
            throw new UserAdminException(UserAdminErrorKind.Conflict, "Нельзя деактивировать или удалить последнего администратора");
    }

    private async Task<bool> IsLastActiveAdminAsync(int userId, CancellationToken ct)
    {
        var isAdmin = await _db.UserRoleMappings
            .AnyAsync(m => m.UserId == userId && m.Role.Code == AdminRoleCode, ct);
        if (!isAdmin) return false;

        var otherActiveAdmins = await _db.Users.CountAsync(u =>
            u.Id != userId && u.IsActive && u.RoleMappings.Any(m => m.Role.Code == AdminRoleCode), ct);
        return otherActiveAdmins == 0;
    }

    private async Task EnsureManagerExistsAsync(int? managerId, int? selfId, CancellationToken ct)
    {
        if (!managerId.HasValue) return;
        if (selfId == managerId)
            throw new UserAdminException(UserAdminErrorKind.Validation, "Пользователь не может быть собственным руководителем");
        if (!await _db.Users.AnyAsync(u => u.Id == managerId.Value, ct))
            throw new UserAdminException(UserAdminErrorKind.Validation, "Руководитель не найден");
    }

    private async Task EnsureTenantExistsAsync(int? tenantId, CancellationToken ct)
    {
        if (!tenantId.HasValue) return;
        if (!await _db.Tenants.AnyAsync(t => t.Id == tenantId.Value, ct))
            throw new UserAdminException(UserAdminErrorKind.Validation, "Тенант не найден");
    }

    private async Task EnsureRolesExistAsync(List<int> roleIds, CancellationToken ct)
    {
        if (roleIds.Count == 0) return;
        var found = await _db.SystemRoles.CountAsync(r => roleIds.Contains(r.Id), ct);
        if (found != roleIds.Count)
            throw new UserAdminException(UserAdminErrorKind.Validation, "Указана несуществующая роль");
    }

    private void AddAudit(int actorId, int userId, string action, object? oldValues, object? newValues, string? ip)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            EntityType = "User",
            EntityId = userId,
            Action = action,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = ip,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static UserAdminDto ToDto(User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        FullName = u.FullName,
        Department = u.Department,
        ManagerId = u.ManagerId,
        TenantId = u.TenantId,
        IsActive = u.IsActive,
        IsManager = u.IsManager,
        IsTwoFactorEnabled = u.IsTwoFactorEnabled,
        Status = u.Status,
        FailedLoginAttempts = u.FailedLoginAttempts,
        LockedUntil = u.LockedUntil,
        CreatedAt = u.CreatedAt,
        RoleIds = u.RoleMappings.Select(m => m.RoleId).OrderBy(x => x).ToList(),
        RoleCodes = u.RoleMappings.Select(m => m.Role?.Code).Where(c => c != null).Select(c => c!).ToList()
    };
}

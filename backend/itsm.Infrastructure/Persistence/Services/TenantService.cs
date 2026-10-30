using System.Text.Json;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Persistence.Services;

public interface ITenantService
{
    Task<List<TenantDto>> GetAllAsync(CancellationToken ct = default);
    Task<TenantDto?> GetByIdAsync(int tenantId, CancellationToken ct = default);
    Task<TenantDto?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<TenantUserQuotaDto?> GetUserQuotaAsync(int tenantId, CancellationToken ct = default);
    Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken ct = default);
    Task<TenantDto?> UpdateAsync(int tenantId, UpdateTenantRequest request, CancellationToken ct = default);
    Task<TenantDto?> UpdateStatusAsync(int tenantId, bool isActive, CancellationToken ct = default);
    Task<TenantDto?> BlockAsync(int tenantId, BlockTenantRequest request, CancellationToken ct = default);
    Task<TenantDto?> UnblockAsync(int tenantId, CancellationToken ct = default);
    Task<bool> DeleteAsync(int tenantId, CancellationToken ct = default);
    Task EnforceExpiredUserLimitsAsync(CancellationToken ct = default);
}

public class TenantService : ITenantService
{
    private static readonly TimeSpan UserLimitGracePeriod = TimeSpan.FromDays(7);
    private const string AdminRoleCode = "admin";
    private const string TenantAdminRoleCode = "tenant_admin";
    private readonly ItsmDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public TenantService(ItsmDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<List<TenantDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tenants = await _db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

        return await ToDtosWithUsageAsync(tenants, ct);
    }

    public async Task<TenantDto?> GetByIdAsync(int tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, cancellationToken: ct);
        if (tenant is null)
            return null;

        return (await ToDtosWithUsageAsync(new[] { tenant }, ct)).Single();
    }

    public async Task<TenantDto?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Code == code, ct);
        return tenant is null ? null : ToDto(tenant);
    }

    public async Task<TenantUserQuotaDto?> GetUserQuotaAsync(int tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == tenantId, ct);
        if (tenant is null)
            return null;

        var activeUsers = await GetActiveUsersAsync(tenantId, ct);
        SynchronizeUserLimitGrace(tenant, activeUsers.Count, DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(ct);

        var overLimit = Math.Max(0, activeUsers.Count - tenant.MaxUsers);
        var eligibleCount = activeUsers.Count(user => !IsProtectedFromQuotaDeactivation(user));
        return new TenantUserQuotaDto(
            tenant.Id,
            tenant.Name,
            activeUsers.Count,
            tenant.MaxUsers,
            overLimit,
            eligibleCount,
            tenant.UserLimitGraceEndsAt);
    }

    public async Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        var adminLogin = request.FirstAdminLogin.Trim();
        var adminEmail = request.FirstAdminEmail.Trim();
        var adminFullName = request.FirstAdminFullName.Trim();

        if (string.IsNullOrEmpty(name))
            throw new InvalidOperationException("Название тенанта обязательно");

        if (string.IsNullOrEmpty(adminLogin))
            throw new InvalidOperationException("Логин администратора обязателен");

        var code = GenerateCode(name);
        if (string.IsNullOrEmpty(code))
            throw new InvalidOperationException("Не удалось сформировать код тенанта из названия");

        if (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Code == code, ct))
            throw new InvalidOperationException($"Тенант с кодом '{code}' уже существует");

        if (await _db.Users.IgnoreQueryFilters().AnyAsync(
                user => user.Username.ToLower() == adminLogin.ToLower()
                        || user.Email.ToLower() == request.FirstAdminEmail.Trim().ToLower(),
                ct))
            throw new InvalidOperationException("Логин или email первого администратора уже используется");

        var adminRoleId = await _db.SystemRoles
            .Where(role => role.Code == "admin")
            .Select(role => (int?)role.Id)
            .FirstOrDefaultAsync(ct);
        if (!adminRoleId.HasValue)
            throw new InvalidOperationException("Системная роль admin не найдена");

        var tenant = new Tenant
        {
            Code = code,
            Name = name,
            IsActive = true,
            MaxUsers = request.MaxUsers ?? 100,
            MaxAssets = request.MaxAssets ?? 1000,
            MaxTickets = request.MaxTickets ?? 10000,
            MaxStorageBytes = request.MaxStorageBytes ?? 10_737_418_240, // 10 GB
            IsBlocked = false,
            CurrentUserCount = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var firstAdmin = new User
        {
            Username = adminLogin,
            Email = adminEmail,
            FullName = adminFullName,
            IsActive = true,
            Status = UserStatuses.Verified,
            PasswordHash = _passwordHasher.Hash(request.FirstAdminPassword),
            PasswordChangedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };
        firstAdmin.RoleMappings.Add(new UserRoleMapping { RoleId = adminRoleId.Value });
        tenant.Users.Add(firstAdmin);

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(tenant.Id, ct))!;
    }

    public async Task<TenantDto?> UpdateAsync(int tenantId, UpdateTenantRequest request, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, cancellationToken: ct);
        if (tenant is null)
            return null;

        if (!string.IsNullOrWhiteSpace(request.Name))
            tenant.Name = request.Name.Trim();

        var userLimitChanged = request.MaxUsers is > 0 && request.MaxUsers.Value != tenant.MaxUsers;
        if (request.MaxUsers.HasValue && request.MaxUsers.Value > 0)
            tenant.MaxUsers = request.MaxUsers.Value;

        if (request.MaxAssets.HasValue && request.MaxAssets.Value > 0)
            tenant.MaxAssets = request.MaxAssets.Value;

        if (request.MaxTickets.HasValue && request.MaxTickets.Value > 0)
            tenant.MaxTickets = request.MaxTickets.Value;

        if (request.MaxStorageBytes.HasValue && request.MaxStorageBytes.Value > 0)
            tenant.MaxStorageBytes = request.MaxStorageBytes.Value;

        var activeUserCount = await _db.Users.IgnoreQueryFilters()
            .CountAsync(user => user.TenantId == tenantId && user.IsActive, ct);
        var now = DateTimeOffset.UtcNow;
        if (activeUserCount <= tenant.MaxUsers)
            tenant.UserLimitGraceEndsAt = null;
        else if (userLimitChanged || tenant.UserLimitGraceEndsAt is null)
            tenant.UserLimitGraceEndsAt = now.Add(UserLimitGracePeriod);

        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(tenant.Id, ct))!;
    }

    public async Task<TenantDto?> UpdateStatusAsync(int tenantId, bool isActive, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, cancellationToken: ct);
        if (tenant is null)
            return null;

        tenant.IsActive = isActive;
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(tenant.Id, ct))!;
    }

    public async Task<TenantDto?> BlockAsync(int tenantId, BlockTenantRequest request, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, cancellationToken: ct);
        if (tenant is null)
            return null;

        tenant.IsBlocked = true;
        tenant.BlockReason = request.Reason;
        tenant.BlockedAt = DateTimeOffset.UtcNow;
        tenant.BlockedUntil = request.BlockedUntil;

        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(tenant.Id, ct))!;
    }

    public async Task<TenantDto?> UnblockAsync(int tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, cancellationToken: ct);
        if (tenant is null)
            return null;

        tenant.IsBlocked = false;
        tenant.BlockReason = null;
        tenant.BlockedAt = null;
        tenant.BlockedUntil = null;

        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(tenant.Id, ct))!;
    }

    public async Task<bool> DeleteAsync(int tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, cancellationToken: ct);
        if (tenant is null)
            return false;

        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.TenantId == tenantId, ct))
            throw new InvalidOperationException("Нельзя удалить тенант с пользователями. Сначала удалите всех пользователей.");

        _db.Tenants.Remove(tenant);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task EnforceExpiredUserLimitsAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var tenants = await _db.Tenants.IgnoreQueryFilters()
            .Where(tenant => tenant.UserLimitGraceEndsAt != null && tenant.UserLimitGraceEndsAt <= now)
            .ToListAsync(ct);

        foreach (var tenant in tenants)
        {
            var activeUsers = await GetActiveUsersAsync(tenant.Id, ct);
            if (activeUsers.Count <= tenant.MaxUsers)
            {
                tenant.UserLimitGraceEndsAt = null;
                continue;
            }

            var usersToDeactivate = activeUsers
                .Where(user => !IsProtectedFromQuotaDeactivation(user))
                .ToList();
            Shuffle(usersToDeactivate);

            var excessCount = activeUsers.Count - tenant.MaxUsers;
            foreach (var user in usersToDeactivate.Take(excessCount))
            {
                user.IsActive = false;
                _db.AuditLogs.Add(new AuditLog
                {
                    EntityType = "User",
                    EntityId = user.Id,
                    Action = "user_auto_deactivated_quota",
                    OldValues = JsonSerializer.Serialize(new { IsActive = true }),
                    NewValues = JsonSerializer.Serialize(new
                    {
                        IsActive = false,
                        TenantId = tenant.Id,
                        Reason = "User limit exceeded after grace period"
                    }),
                    CreatedAt = now
                });
            }

            var remainingActiveUsers = activeUsers.Count - Math.Min(excessCount, usersToDeactivate.Count);
            tenant.CurrentUserCount = remainingActiveUsers;
            if (remainingActiveUsers <= tenant.MaxUsers)
                tenant.UserLimitGraceEndsAt = null;
        }

        await _db.SaveChangesAsync(ct);
    }

    private static TenantDto ToDto(Tenant tenant) => new()
    {
        Id = tenant.Id,
        Code = tenant.Code,
        Name = tenant.Name,
        IsActive = tenant.IsActive,
        CreatedAt = tenant.CreatedAt,
        MaxUsers = tenant.MaxUsers,
        MaxAssets = tenant.MaxAssets,
        MaxTickets = tenant.MaxTickets,
        MaxStorageBytes = tenant.MaxStorageBytes,
        IsBlocked = tenant.IsBlocked,
        BlockReason = tenant.BlockReason,
        BlockedAt = tenant.BlockedAt,
        BlockedUntil = tenant.BlockedUntil,
        CurrentUserCount = tenant.CurrentUserCount,
        CurrentAssetCount = tenant.CurrentAssetCount,
        CurrentTicketCount = tenant.CurrentTicketCount,
        CurrentStorageBytes = tenant.CurrentStorageBytes
    };

    private async Task<List<TenantDto>> ToDtosWithUsageAsync(
        IReadOnlyCollection<Tenant> tenants,
        CancellationToken ct)
    {
        var tenantIds = tenants.Select(tenant => tenant.Id).ToArray();
        if (tenantIds.Length == 0)
            return new List<TenantDto>();

        var userCounts = await _db.Users
            .IgnoreQueryFilters()
            .Where(user => tenantIds.Contains(user.TenantId) && user.IsActive)
            .GroupBy(user => user.TenantId)
            .Select(group => new { TenantId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.TenantId, group => group.Count, ct);

        var assetCounts = await _db.Assets
            .IgnoreQueryFilters()
            .Where(asset => tenantIds.Contains(asset.TenantId))
            .GroupBy(asset => asset.TenantId)
            .Select(group => new { TenantId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.TenantId, group => group.Count, ct);

        var ticketCounts = await _db.Tickets
            .IgnoreQueryFilters()
            .Where(ticket => tenantIds.Contains(ticket.TenantId))
            .GroupBy(ticket => ticket.TenantId)
            .Select(group => new { TenantId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.TenantId, group => group.Count, ct);

        var storageByTenant = await _db.Attachments
            .IgnoreQueryFilters()
            .Where(attachment => tenantIds.Contains(attachment.TicketMessage.Ticket.TenantId))
            .GroupBy(attachment => attachment.TicketMessage.Ticket.TenantId)
            .Select(group => new { TenantId = group.Key, Bytes = group.Sum(attachment => attachment.SizeBytes) })
            .ToDictionaryAsync(group => group.TenantId, group => group.Bytes, ct);

        return tenants.Select(tenant =>
        {
            var dto = ToDto(tenant);
            dto.CurrentUserCount = userCounts.GetValueOrDefault(tenant.Id);
            dto.CurrentAssetCount = assetCounts.GetValueOrDefault(tenant.Id);
            dto.CurrentTicketCount = ticketCounts.GetValueOrDefault(tenant.Id);
            dto.CurrentStorageBytes = storageByTenant.GetValueOrDefault(tenant.Id);
            return dto;
        }).ToList();
    }

    private async Task<List<User>> GetActiveUsersAsync(int tenantId, CancellationToken ct)
    {
        return await _db.Users
            .IgnoreQueryFilters()
            .Where(user => user.TenantId == tenantId && user.IsActive)
            .Include(user => user.RoleMappings)
            .ThenInclude(mapping => mapping.Role)
            .ToListAsync(ct);
    }

    private static void SynchronizeUserLimitGrace(Tenant tenant, int activeUserCount, DateTimeOffset now)
    {
        if (activeUserCount <= tenant.MaxUsers)
            tenant.UserLimitGraceEndsAt = null;
        else
            tenant.UserLimitGraceEndsAt ??= now.Add(UserLimitGracePeriod);
    }

    private static bool IsProtectedFromQuotaDeactivation(User user)
    {
        return user.RoleMappings.Any(mapping =>
            mapping.Role.Code == AdminRoleCode || mapping.Role.Code == TenantAdminRoleCode);
    }

    private static void Shuffle<T>(IList<T> items)
    {
        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }

    private static string GenerateCode(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace(".", "-")
            .Trim('-');
    }
}

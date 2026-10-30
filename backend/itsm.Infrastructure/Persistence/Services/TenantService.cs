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
    Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken ct = default);
    Task<TenantDto?> UpdateAsync(int tenantId, UpdateTenantRequest request, CancellationToken ct = default);
    Task<TenantDto?> UpdateStatusAsync(int tenantId, bool isActive, CancellationToken ct = default);
    Task<TenantDto?> BlockAsync(int tenantId, BlockTenantRequest request, CancellationToken ct = default);
    Task<TenantDto?> UnblockAsync(int tenantId, CancellationToken ct = default);
    Task<bool> DeleteAsync(int tenantId, CancellationToken ct = default);
}

public class TenantService : ITenantService
{
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

        if (await _db.Tenants.AnyAsync(t => t.Code == code, ct))
            throw new InvalidOperationException($"Тенант с кодом '{code}' уже существует");

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

        if (request.MaxUsers.HasValue && request.MaxUsers.Value > 0)
            tenant.MaxUsers = request.MaxUsers.Value;

        if (request.MaxAssets.HasValue && request.MaxAssets.Value > 0)
            tenant.MaxAssets = request.MaxAssets.Value;

        if (request.MaxTickets.HasValue && request.MaxTickets.Value > 0)
            tenant.MaxTickets = request.MaxTickets.Value;

        if (request.MaxStorageBytes.HasValue && request.MaxStorageBytes.Value > 0)
            tenant.MaxStorageBytes = request.MaxStorageBytes.Value;

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

    private static string GenerateCode(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace(".", "-")
            .Trim('-');
    }
}

using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
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

    public TenantService(ItsmDbContext db)
    {
        _db = db;
    }

    public async Task<List<TenantDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Tenants
            .OrderBy(t => t.CreatedAt)
            .Select(t => ToDto(t))
            .ToListAsync(ct);
    }

    public async Task<TenantDto?> GetByIdAsync(int tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, cancellationToken: ct);
        return tenant is null ? null : ToDto(tenant);
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

        if (string.IsNullOrEmpty(name))
            throw new InvalidOperationException("Название тенанта обязательно");

        if (string.IsNullOrEmpty(adminLogin))
            throw new InvalidOperationException("Логин администратора обязателен");

        if (await _db.Tenants.AnyAsync(t => t.Code == name.ToLowerInvariant(), ct))
            throw new InvalidOperationException($"Тенант с кодом '{name.ToLowerInvariant()}' уже существует");

        var tenant = new Tenant
        {
            Code = GenerateCode(name),
            Name = name,
            IsActive = true,
            MaxUsers = request.MaxUsers ?? 100,
            MaxAssets = request.MaxAssets ?? 1000,
            MaxTickets = request.MaxTickets ?? 10000,
            MaxStorageBytes = request.MaxStorageBytes ?? 10_737_418_240, // 10 GB
            IsBlocked = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        return ToDto(tenant);
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
        return ToDto(tenant);
    }

    public async Task<TenantDto?> UpdateStatusAsync(int tenantId, bool isActive, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, cancellationToken: ct);
        if (tenant is null)
            return null;

        tenant.IsActive = isActive;
        await _db.SaveChangesAsync(ct);

        return ToDto(tenant);
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
        return ToDto(tenant);
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
        return ToDto(tenant);
    }

    public async Task<bool> DeleteAsync(int tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, cancellationToken: ct);
        if (tenant is null)
            return false;

        if (await _db.Users.AnyAsync(u => u.TenantId == tenantId, ct))
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

    private static string GenerateCode(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace(".", "-")
            .Trim('-');
    }
}

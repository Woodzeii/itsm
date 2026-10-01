using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using itsm.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Identity;

public class UserService : IUserService
{
    private readonly ItsmDbContext _db;

    public UserService(ItsmDbContext db)
    {
        _db = db;
    }

    public Task<bool> ExistsByUsernameOrEmailAsync(string username, string email, CancellationToken ct = default)
        => _db.Users.AnyAsync(u => u.Username == username || u.Email == email, ct);

    public async Task<int> CreateAsync(
        RegisterRequest request,
        string passwordHash,
        string verificationTokenHash,
        DateTimeOffset tokenExpiresAt,
        CancellationToken ct = default)
    {
        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            FullName = request.FullName ?? string.Empty,
            Department = request.Department,
            Status = UserStatuses.Unverified,
            PasswordHash = passwordHash,
            VerificationTokenHash = verificationTokenHash,
            VerificationTokenExpiresAt = tokenExpiresAt,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        var portalRoleId = await _db.SystemRoles
            .Where(r => r.Code == "portal_user")
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync(ct);

        if (portalRoleId.HasValue)
        {
            _db.UserRoleMappings.Add(new UserRoleMapping
            {
                UserId = user.Id,
                RoleId = portalRoleId.Value
            });
            await _db.SaveChangesAsync(ct);
        }

        return user.Id;
    }

    public async Task<ConfirmEmailResult> ConfirmEmailAsync(string plainToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(plainToken))
            return new ConfirmEmailResult { Status = ConfirmEmailStatus.InvalidToken, Message = "Токен обязателен" };

        var tokenHash = TokenGenerator.Hash(plainToken);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.VerificationTokenHash == tokenHash, ct);

        if (user is null)
            return new ConfirmEmailResult { Status = ConfirmEmailStatus.InvalidToken, Message = "Неверный токен" };

        if (user.Status == UserStatuses.Verified)
            return new ConfirmEmailResult { Status = ConfirmEmailStatus.AlreadyVerified, Message = "Email уже подтверждён" };

        if (user.VerificationTokenExpiresAt is null || user.VerificationTokenExpiresAt < DateTimeOffset.UtcNow)
            return new ConfirmEmailResult { Status = ConfirmEmailStatus.Expired, Message = "Токен истёк" };

        user.Status = UserStatuses.Verified;
        user.VerificationTokenHash = null;
        user.VerificationTokenExpiresAt = null;

        await _db.SaveChangesAsync(ct);

        return new ConfirmEmailResult { Status = ConfirmEmailStatus.Success, Message = "Email подтверждён" };
    }

    public async Task<List<string>> GetRolesAsync(int userId, CancellationToken ct = default)
    {
        return await _db.UserRoleMappings
            .Where(m => m.UserId == userId)
            .Select(m => m.Role.Code!)
            .Where(code => code != null)
            .ToListAsync(ct);
    }

    public async Task<int?> FindIdByUsernameAsync(string username, CancellationToken ct = default)
    {
        return await _db.Users
            .Where(u => u.Username == username)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);
    }
}
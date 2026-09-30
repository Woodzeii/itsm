using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
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

        var clientRoleId = await _db.SystemRoles
            .Where(r => r.Code == "client")
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync(ct);

        if (clientRoleId.HasValue)
        {
            _db.UserRoleMappings.Add(new UserRoleMapping
            {
                UserId = user.Id,
                RoleId = clientRoleId.Value
            });
            await _db.SaveChangesAsync(ct);
        }

        return user.Id;
    }
}
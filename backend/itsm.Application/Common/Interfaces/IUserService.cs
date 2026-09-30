using itsm.Application.Common.Models;

namespace itsm.Application.Common.Interfaces;

public interface IUserService
{
    Task<bool> ExistsByUsernameOrEmailAsync(string username, string email, CancellationToken ct = default);

    Task<int> CreateAsync(
        RegisterRequest request,
        string passwordHash,
        string verificationTokenHash,
        DateTimeOffset tokenExpiresAt,
        CancellationToken ct = default);
}
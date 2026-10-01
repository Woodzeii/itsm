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

    Task<ConfirmEmailResult> ConfirmEmailAsync(string plainToken, CancellationToken ct = default);

    /// <summary>Возвращает коды ролей пользователя (например, ["admin", "manager"]).</summary>
    Task<List<string>> GetRolesAsync(int userId, CancellationToken ct = default);

    /// <summary>Возвращает реальный users.id по username, или null если не найден.</summary>
    Task<int?> FindIdByUsernameAsync(string username, CancellationToken ct = default);
}
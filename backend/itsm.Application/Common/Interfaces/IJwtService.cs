namespace itsm.Application.Common.Interfaces;

public interface IJwtService
{
    /// <summary>
    /// Выпускает JWT пользователя.
    /// Все роли всегда попадают в JSON-claim "roles".
    /// Если <paramref name="activeRole"/> задан, только он попадает в Role-claims
    /// (для [Authorize(Roles = "...")]) и в claim "activeRole".
    /// Без activeRole мультиролевой токен не содержит Role-claims, пока роль не выбрана.
    /// </summary>
    string GenerateToken(
        int userId,
        string login,
        string email,
        IEnumerable<string> roles,
        string? activeRole = null);
}
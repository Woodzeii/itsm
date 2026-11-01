namespace itsm.Application.Common.Models;

/// <summary>
/// Текущая сессия пользователя: источник истины для выбора активной роли на фронтенде.
/// </summary>
public class SessionResponse
{
    public int UserId { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();

    /// <summary>Активная роль текущей сессии (из claim "activeRole") или null.</summary>
    public string? ActiveRole { get; set; }

    /// <summary>true, если у пользователя несколько ролей и активная ещё не выбрана.</summary>
    public bool RequiresRoleSelection { get; set; }
}

public class SelectRoleRequest
{
    public string Role { get; set; } = string.Empty;
}

public class SelectRoleResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public string ActiveRole { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace itsm.Infrastructure.Identity;

/// <summary>
/// Генерация JWT-токенов. Параметры подписи берутся из JwtSettings.
/// Все роли упаковываются в Claims:
/// - Как массив в custom claim "roles" (для валидации на фронтенде)
/// - Как отдельные claims с типом Role (для [Authorize(Roles = "")] атрибутов)
/// </summary>
public class JwtService : IJwtService
{
    private readonly JwtSettings _settings;

    public JwtService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public string GenerateToken(int userId, string login, string email, int tenantId, IEnumerable<string> roles)
    {
        var rolesList = roles.ToList();

        var claims = new List<Claim>
        {
            new("Id", userId.ToString()),
            new("Login", login),
            new("Email", email),
            new("tenant_id", tenantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        // Роли — чтобы [Authorize(Roles = "...")] работал
        foreach (var role in rolesList)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Упаковываем все роли как JSON-массив в custom claim "roles"
        // Фронтенд может парсить это и валидировать доступные роли на этапе сессии
        var rolesJson = JsonSerializer.Serialize(rolesList);
        claims.Add(new Claim("roles", rolesJson));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.ExpiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
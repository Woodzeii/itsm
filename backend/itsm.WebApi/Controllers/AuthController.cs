using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ILdapService _ldap;
    private readonly ITwoFactorService _twoFactor;
    private readonly IJwtService _jwt;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ILdapService ldap,
        ITwoFactorService twoFactor,
        IJwtService jwt,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthController> logger)
    {
        _ldap = ldap;
        _twoFactor = twoFactor;
        _jwt = jwt;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Логин и пароль обязательны" });

        var isValid = await _ldap.ValidateCredentialsAsync(request.Username, request.Password);
        if (!isValid)
        {
            _logger.LogWarning("Неудачная попытка входа для {Username}", request.Username);
            return Unauthorized(new { message = "Неверный логин или пароль" });
        }

        var ldapUser = await _ldap.FindUserAsync(request.Username);
        if (ldapUser is null)
            return Unauthorized(new { message = "Пользователь не найден" });

        var userId = Math.Abs(ldapUser.Username.GetHashCode());

        var code = _twoFactor.GenerateCode(userId, ldapUser.Username, ldapUser.Email);

        _logger.LogWarning("DEV ONLY: 2FA код для {Username}: {Code}", request.Username, code);

        return Ok(new LoginResponse
        {
            RequiresTwoFactor = true,
            UserId = userId,
            Message = "Введите 6-значный код из приложения-аутентификатора"
        });
    }
    [AllowAnonymous]
    [HttpPost("verify-2fa")]
    public IActionResult VerifyTwoFactor([FromBody] VerifyTwoFactorRequest request)
    {
        if (request.UserId <= 0 || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { message = "UserId и Code обязательны" });

        var userData = _twoFactor.ValidateCode(request.UserId, request.Code);
        if (userData is null)
        {
            _logger.LogWarning("2FA: неверный или истёкший код для userId={UserId}", request.UserId);
            return Unauthorized(new { message = "Неверный или истёкший код" });
        }

        var token = _jwt.GenerateToken(userData.UserId, userData.Login, userData.Email);

        _logger.LogInformation("JWT выпущен для {Login} (userId={UserId})",
            userData.Login, userData.UserId);

        return Ok(new VerifyTwoFactorResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = _jwtSettings.ExpiresMinutes * 60
        });
    }
}
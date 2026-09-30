using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ILdapService _ldap;
    private readonly ITwoFactorService _twoFactor;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ILdapService ldap,
        ITwoFactorService twoFactor,
        ILogger<AuthController> logger)
    {
        _ldap = ldap;
        _twoFactor = twoFactor;
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

        var code = _twoFactor.GenerateCode(userId);

        _logger.LogWarning("DEV ONLY: 2FA код для {Username}: {Code}", request.Username, code);

        return Ok(new LoginResponse
        {
            RequiresTwoFactor = true,
            UserId = userId,
            Message = "Введите 6-значный код из приложения-аутентификатора"
        });
    }
}
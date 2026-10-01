using FluentValidation;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Infrastructure.Security;
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
    private readonly IUserService _userService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _email;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ILdapService ldap,
        ITwoFactorService twoFactor,
        IJwtService jwt,
        IUserService userService,
        IPasswordHasher passwordHasher,
        IEmailService email,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthController> logger)
    {
        _ldap = ldap;
        _twoFactor = twoFactor;
        _jwt = jwt;
        _userService = userService;
        _passwordHasher = passwordHasher;
        _email = email;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    // ================= REGISTER =================

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        [FromServices] IValidator<RegisterRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { message = "Ошибка валидации", errors });
        }

        var exists = await _userService.ExistsByUsernameOrEmailAsync(request.Username, request.Email, ct);
        if (exists)
            return Conflict(new { message = "Пользователь с таким логином или email уже зарегистрирован" });

        var passwordHash = _passwordHasher.Hash(request.Password);
        var (plainToken, tokenHash) = TokenGenerator.Generate();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);

        var userId = await _userService.CreateAsync(request, passwordHash, tokenHash, expiresAt, ct);

        await _email.SendVerificationEmailAsync(request.Email, request.FullName, plainToken, ct);

        _logger.LogInformation("Зарегистрирован пользователь {Username} (id={UserId})", request.Username, userId);

        return Ok(new RegisterResponse
        {
            UserId = userId,
            Username = request.Username,
            Email = request.Email,
            Status = UserStatuses.Unverified,
            Message = "Регистрация успешна. Подтвердите email."
        });
    }

    // ================= CONFIRM EMAIL =================

    [AllowAnonymous]
    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { message = "Токен обязателен" });

        var result = await _userService.ConfirmEmailAsync(request.Token, ct);

        return result.Status switch
        {
            ConfirmEmailStatus.Success => Ok(new ConfirmEmailResponse { Message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    // ================= LOGIN =================

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
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

        // Ищем реальный id в БД: он нужен для ролей и 2FA.
        // Если пользователя нет — fallback на хеш (для LDAP без синхронизации с БД).
        var userId = await _userService.FindIdByUsernameAsync(ldapUser.Username, HttpContext.RequestAborted)
            ?? Math.Abs(ldapUser.Username.GetHashCode());

        var code = _twoFactor.GenerateCode(userId, ldapUser.Username, ldapUser.Email);

        _logger.LogWarning("DEV ONLY: 2FA код для {Username}: {Code}", request.Username, code);

        return Ok(new LoginResponse
        {
            RequiresTwoFactor = true,
            UserId = userId,
            Message = "Введите 6-значный код из приложения-аутентификатора"
        });
    }

    // ================= VERIFY 2FA =================

    [AllowAnonymous]
    [HttpPost("verify-2fa")]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest request)
    {
        if (request.UserId <= 0 || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { message = "UserId и Code обязательны" });

        var userData = _twoFactor.ValidateCode(request.UserId, request.Code);
        if (userData is null)
        {
            _logger.LogWarning("2FA: неверный или истёкший код для userId={UserId}", request.UserId);
            return Unauthorized(new { message = "Неверный или истёкший код" });
        }

        var roles = await _userService.GetRolesAsync(userData.UserId, HttpContext.RequestAborted);
        var token = _jwt.GenerateToken(userData.UserId, userData.Login, userData.Email, roles);

        _logger.LogInformation("JWT выпущен для {Login} (userId={UserId}, роли: {Roles})",
            userData.Login, userData.UserId, string.Join(", ", roles));

        return Ok(new VerifyTwoFactorResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = _jwtSettings.ExpiresMinutes * 60
        });
    }
}
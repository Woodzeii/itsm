using FluentValidation;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Infrastructure.Persistence;
using itsm.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string Issuer = "ITSM NC";

    private readonly ITwoFactorService _twoFactor;
    private readonly ITotpService _totp;
    private readonly IJwtService _jwt;
    private readonly IUserService _userService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _email;
    private readonly ItsmDbContext _db;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ITwoFactorService twoFactor,
        ITotpService totp,
        IJwtService jwt,
        IUserService userService,
        IPasswordHasher passwordHasher,
        IEmailService email,
        ItsmDbContext db,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthController> logger)
    {
        _twoFactor = twoFactor;
        _totp = totp;
        _jwt = jwt;
        _userService = userService;
        _passwordHasher = passwordHasher;
        _email = email;
        _db = db;
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
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Логин и пароль обязательны" });

        // Ищем пользователя в БД
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username, ct);

        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Неудачная попытка входа для {Username}", request.Username);
            return Unauthorized(new { message = "Неверный логин или пароль" });
        }

        // Проверяем пароль через BCrypt
        if (string.IsNullOrEmpty(user.PasswordHash)
            || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Неудачная попытка входа для {Username}", request.Username);
            return Unauthorized(new { message = "Неверный логин или пароль" });
        }

        // TOTP-flow: если у пользователя включена 2FA — требуем код из приложения
        if (user.IsTwoFactorEnabled && !string.IsNullOrEmpty(user.TwoFaSecret))
        {
            _logger.LogInformation("Логин для {Login} требует TOTP-код", user.Username);

            return Ok(new LoginResponse
            {
                RequiresTwoFactor = true,
                UserId = user.Id,
                Message = "Введите 6-значный код из приложения-аутентификатора"
            });
        }

        // Fallback: email-код (для первого входа, пока TOTP не настроен)
        var code = _twoFactor.GenerateCode(user.Id, user.Username, user.Email);

        _logger.LogWarning("DEV ONLY: 2FA код для {Username}: {Code}", request.Username, code);

        return Ok(new LoginResponse
        {
            RequiresTwoFactor = true,
            UserId = user.Id,
            Message = "Введите 6-значный код из письма/логов"
        });
    }

    // ================= VERIFY 2FA =================

    [AllowAnonymous]
    [HttpPost("verify-2fa")]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest request)
    {
        if (request.UserId <= 0 || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { message = "UserId и Code обязательны" });

        var dbUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, HttpContext.RequestAborted);

        // 1) TOTP-ветка: только если пользователь найден и у него явно включена 2FA.
        if (dbUser is not null
            && dbUser.IsTwoFactorEnabled
            && !string.IsNullOrEmpty(dbUser.TwoFaSecret))
        {
            if (!_totp.VerifyCode(dbUser.TwoFaSecret, request.Code))
            {
                _logger.LogWarning("2FA (TOTP): неверный код для userId={UserId}", request.UserId);
                return Unauthorized(new { message = "Неверный или истёкший код" });
            }

            var rolesTotp = await _userService.GetRolesAsync(dbUser.Id, HttpContext.RequestAborted);
            var tokenTotp = _jwt.GenerateToken(dbUser.Id, dbUser.Username, dbUser.Email, rolesTotp);

            _logger.LogInformation("JWT выпущен для {Login} через TOTP (userId={UserId})",
                dbUser.Username, dbUser.Id);

            return Ok(new VerifyTwoFactorResponse
            {
                AccessToken = tokenTotp,
                TokenType = "Bearer",
                ExpiresIn = _jwtSettings.ExpiresMinutes * 60
            });
        }

        // 2) Fallback: email-код через кэш.
        var userData = _twoFactor.ValidateCode(request.UserId, request.Code);
        if (userData is null)
        {
            _logger.LogWarning("2FA (email): неверный или истёкший код для userId={UserId}", request.UserId);
            return Unauthorized(new { message = "Неверный или истёкший код" });
        }

        var roles = await _userService.GetRolesAsync(userData.UserId, HttpContext.RequestAborted);
        var token = _jwt.GenerateToken(userData.UserId, userData.Login, userData.Email, roles);

        _logger.LogInformation("JWT выпущен для {Login} через email-код (userId={UserId})",
            userData.Login, userData.UserId);

        return Ok(new VerifyTwoFactorResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = _jwtSettings.ExpiresMinutes * 60
        });
    }

    // ================= 2FA: SETUP =================

    [Authorize]
    [HttpPost("2fa/setup")]
    public async Task<IActionResult> SetupTwoFactor()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user is null) return NotFound();

        if (user.IsTwoFactorEnabled)
            return Conflict(new { message = "2FA уже включена. Сначала отключите её." });

        var secret = _totp.GenerateSecret();
        user.TwoFaSecret = secret;
        await _db.SaveChangesAsync();

        var uri = _totp.BuildOtpAuthUri(Issuer, user.Username, secret);

        return Ok(new TwoFactorSetupResponse
        {
            Secret = secret,
            OtpAuthUri = uri,
            Issuer = Issuer
        });
    }

    [Authorize]
    [HttpPost("2fa/verify-setup")]
    public async Task<IActionResult> VerifySetup([FromBody] VerifyTwoFactorSetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { message = "Код обязателен" });

        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user is null) return NotFound();

        if (user.IsTwoFactorEnabled)
            return Conflict(new { message = "2FA уже включена" });

        if (string.IsNullOrEmpty(user.TwoFaSecret))
            return BadRequest(new { message = "Секрет не сгенерирован. Сначала вызовите /2fa/setup." });

        if (!_totp.VerifyCode(user.TwoFaSecret, request.Code))
            return BadRequest(new { message = "Неверный код. Проверьте время на устройстве." });

        user.IsTwoFactorEnabled = true;
        user.TwoFactorEnabledAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("2FA (TOTP) включена для пользователя {Login}", user.Username);

        return Ok(new { message = "2FA успешно включена" });
    }

    [Authorize]
    [HttpGet("2fa/status")]
    public async Task<IActionResult> GetTwoFactorStatus()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user is null) return NotFound();

        return Ok(new TwoFactorStatusResponse
        {
            IsEnabled = user.IsTwoFactorEnabled,
            EnabledAt = user.TwoFactorEnabledAt
        });
    }

    [Authorize]
    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor([FromBody] DisableTwoFactorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Пароль обязателен" });

        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user is null) return NotFound();

        // Проверяем пароль через BCrypt
        if (string.IsNullOrEmpty(user.PasswordHash)
            || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Неверный пароль" });
        }

        user.IsTwoFactorEnabled = false;
        user.TwoFaSecret = null;
        user.TwoFactorEnabledAt = null;
        await _db.SaveChangesAsync();

        _logger.LogWarning("2FA (TOTP) отключена для пользователя {Login}", user.Username);

        return Ok(new { message = "2FA отключена" });
    }

    // ================= HELPERS =================

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("Id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        return int.TryParse(claim, out var id) ? id : null;
    }
}
using FluentValidation;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Infrastructure.Persistence;
using itsm.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.Json;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string Issuer = "ITSM NC";
    private static readonly TimeSpan LoginChallengeLifetime = TimeSpan.FromMinutes(5);

    private readonly ITotpService _totp;
    private readonly IJwtService _jwt;
    private readonly IUserService _userService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _email;
    private readonly ItsmDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ITotpService totp,
        IJwtService jwt,
        IUserService userService,
        IPasswordHasher passwordHasher,
        IEmailService email,
        ItsmDbContext db,
        IMemoryCache cache,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthController> logger)
    {
        _totp = totp;
        _jwt = jwt;
        _userService = userService;
        _passwordHasher = passwordHasher;
        _email = email;
        _db = db;
        _cache = cache;
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

        var currentTenantId = _db.GetCurrentTenantId();
        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == currentTenantId, ct);
        if (tenant is null)
            return Conflict(new { message = "Тенант не найден" });

        var activeUserCount = await _db.Users.IgnoreQueryFilters()
            .CountAsync(user => user.TenantId == currentTenantId && user.IsActive, ct);
        if (activeUserCount >= tenant.MaxUsers)
            return Conflict(new
            {
                message = $"Достигнут лимит активных пользователей ({tenant.MaxUsers}). Регистрация временно недоступна."
            });

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
            .IgnoreQueryFilters()
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

        var roles = await _userService.GetRolesAsync(user.Id, ct);
        var isPortalOnlyUser = roles.Count == 1 && roles[0] == "portal_user";
        var tenant = user.TenantId.HasValue
            ? await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == user.TenantId.Value, ct)
            : null;
        var twoFactorMode = tenant?.TwoFactorMode ?? "all";
        var isPrivileged = roles.Any(role =>
            role is "admin" or "tenant_owner" or "tenant_admin");
        var isTwoFactorRequired = !isPortalOnlyUser
            && (twoFactorMode == "all" || (twoFactorMode == "admins" && isPrivileged));
        var isOptional = twoFactorMode == "optional";

        // Existing TOTP enrollment: require the authenticator code at every login.
        if (user.IsTwoFactorEnabled && !string.IsNullOrEmpty(user.TwoFaSecret))
        {
            _logger.LogInformation("Логин для {Login} требует TOTP-код", user.Username);

            return Ok(new LoginResponse
            {
                RequiresTwoFactor = true,
                UserId = user.Id,
                ChallengeId = CreateLoginChallenge(user.Id, isSetup: false),
                Message = "Введите 6-значный код из приложения-аутентификатора"
            });
        }

        if (isOptional || (!isTwoFactorRequired && !isPortalOnlyUser))
        {
            var token = CreateTokenResponse(user, roles);
            return Ok(new LoginResponse
            {
                AccessToken = token.AccessToken,
                TokenType = token.TokenType,
                ExpiresIn = token.ExpiresIn,
                Message = "Вход выполнен"
            });
        }

        var graceDays = tenant?.TwoFactorGraceDays ?? 0;
        var now = DateTimeOffset.UtcNow;
        if (user.TwoFactorGraceEndsAt is null && graceDays > 0)
        {
            user.TwoFactorGraceEndsAt = user.CreatedAt.AddDays(graceDays);
            await _db.SaveChangesAsync(ct);
        }
        if (graceDays > 0
            && user.TwoFactorGraceEndsAt.HasValue
            && user.TwoFactorGraceEndsAt.Value > now)
        {
            var token = CreateTokenResponse(user, roles);
            return Ok(new LoginResponse
            {
                AccessToken = token.AccessToken,
                TokenType = token.TokenType,
                ExpiresIn = token.ExpiresIn,
                TwoFactorGraceEndsAt = user.TwoFactorGraceEndsAt,
                Message = $"Настройте 2FA до {user.TwoFactorGraceEndsAt:dd.MM.yyyy}"
            });
        }
        user.TwoFactorGraceEndsAt = null;

        // Keep an unconfirmed secret stable so a repeated login does not invalidate the
        // QR code already shown to the user.
        if (string.IsNullOrEmpty(user.TwoFaSecret))
            user.TwoFaSecret = _totp.GenerateSecret();

        if (user.IsTwoFactorEnabled)
        {
            user.IsTwoFactorEnabled = false;
            user.TwoFactorEnabledAt = null;
        }
        await _db.SaveChangesAsync(ct);

        var otpAuthUri = _totp.BuildOtpAuthUri(Issuer, user.Username, user.TwoFaSecret);
        return Ok(new LoginResponse
        {
            RequiresTwoFactor = true,
            RequiresTwoFactorSetup = true,
            CanSkipTwoFactorSetup = isPortalOnlyUser,
            UserId = user.Id,
            ChallengeId = CreateLoginChallenge(user.Id, isSetup: true),
            Secret = user.TwoFaSecret,
            OtpAuthUri = otpAuthUri,
            QrCodeImageBase64 = _totp.GenerateQrCodePngBase64(otpAuthUri),
            Message = isPortalOnlyUser
                ? "Настройте 2FA или пропустите этот шаг."
                : "Для продолжения настройте 2FA и подтвердите код из приложения."
        });
    }

    // ================= VERIFY 2FA =================

    [AllowAnonymous]
    [HttpPost("verify-2fa")]
    public async Task<IActionResult> VerifyTwoFactor(
        [FromBody] VerifyTwoFactorRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ChallengeId) || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { message = "ChallengeId и Code обязательны" });

        if (!TryGetLoginChallenge(request.ChallengeId, out var challenge))
            return Unauthorized(new { message = "Сессия входа истекла. Начните вход заново." });

        var dbUser = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == challenge.UserId, ct);
        if (dbUser is null || !dbUser.IsActive || string.IsNullOrEmpty(dbUser.TwoFaSecret))
            return Unauthorized(new { message = "Сессия входа недействительна. Начните вход заново." });

        if (!challenge.IsSetup && !dbUser.IsTwoFactorEnabled)
            return Unauthorized(new { message = "Сессия входа недействительна. Начните вход заново." });

        if (request.UseRecoveryCode)
        {
            if (!TryConsumeRecoveryCode(dbUser, request.Code))
            {
                _logger.LogWarning("2FA: неверный или использованный резервный код для userId={UserId}", dbUser.Id);
                return Unauthorized(new { message = "Неверный или уже использованный резервный код" });
            }
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            if (!_totp.VerifyCode(dbUser.TwoFaSecret, request.Code))
            {
                _logger.LogWarning("2FA (TOTP): неверный код для userId={UserId}", dbUser.Id);
                return Unauthorized(new { message = "Неверный или истёкший код" });
            }
        }

        if (challenge.IsSetup && !dbUser.IsTwoFactorEnabled)
        {
            dbUser.IsTwoFactorEnabled = true;
            dbUser.TwoFactorEnabledAt = DateTimeOffset.UtcNow;
            var recoveryCodes = GenerateRecoveryCodes();
            dbUser.RecoveryCodesJson = JsonSerializer.Serialize(recoveryCodes.Select(HashRecoveryCode).ToList());
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("2FA (TOTP) включена для пользователя {Login}", dbUser.Username);
            var setupToken = CreateTokenResponse(
                dbUser,
                await _userService.GetRolesAsync(dbUser.Id, ct));
            setupToken.RecoveryCodes = recoveryCodes;
            _cache.Remove(GetLoginChallengeKey(request.ChallengeId));
            return Ok(setupToken);
        }

        _cache.Remove(GetLoginChallengeKey(request.ChallengeId));
        return Ok(CreateTokenResponse(
            dbUser,
            await _userService.GetRolesAsync(dbUser.Id, ct)));
    }

    [AllowAnonymous]
    [HttpPost("skip-2fa-setup")]
    public async Task<IActionResult> SkipTwoFactorSetup(
        [FromBody] SkipTwoFactorSetupRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ChallengeId))
            return BadRequest(new { message = "ChallengeId обязателен" });

        if (!TryGetLoginChallenge(request.ChallengeId, out var challenge) || !challenge.IsSetup)
            return Unauthorized(new { message = "Сессия входа истекла. Начните вход заново." });

        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == challenge.UserId, ct);
        if (user is null || !user.IsActive)
            return Unauthorized(new { message = "Сессия входа недействительна. Начните вход заново." });

        var roles = await _userService.GetRolesAsync(user.Id, ct);
        var isPortalOnlyUser = roles.Count == 1 && roles[0] == "portal_user";
        var tenant = user.TenantId.HasValue
            ? await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == user.TenantId.Value, ct)
            : null;
        var mode = tenant?.TwoFactorMode ?? "all";
        var privileged = roles.Any(role => role is "admin" or "tenant_owner" or "tenant_admin");
        var required = !isPortalOnlyUser && (mode == "all" || (mode == "admins" && privileged));
        var graceActive = user.TwoFactorGraceEndsAt.HasValue && user.TwoFactorGraceEndsAt.Value > DateTimeOffset.UtcNow;
        if (required && !isPortalOnlyUser && !graceActive)
            return Forbid();
        if (mode == "optional" || isPortalOnlyUser)
        {
            // Optional and portal users may explicitly skip setup.
        }
        else if (!graceActive)
        {
            return Forbid();
        }

        user.TwoFaSecret = null;
        await _db.SaveChangesAsync(ct);
        _cache.Remove(GetLoginChallengeKey(request.ChallengeId));

        return Ok(CreateTokenResponse(user, roles));
    }

    // ================= SESSION / ACTIVE ROLE =================

    /// <summary>
    /// Возвращает роли текущего пользователя из БД и признак необходимости выбора активной роли.
    /// </summary>
    [Authorize]
    [HttpGet("session")]
    public async Task<IActionResult> GetSession(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (user is null || !user.IsActive)
            return Unauthorized(new { message = "Сессия недействительна. Начните вход заново." });

        var roles = await _userService.GetRolesAsync(user.Id, ct);
        var activeRole = ResolveActiveRole(User.FindFirst("activeRole")?.Value, roles);

        return Ok(new SessionResponse
        {
            UserId = user.Id,
            Login = user.Username,
            Email = user.Email,
            Roles = roles,
            ActiveRole = activeRole,
            RequiresRoleSelection = roles.Count > 1 && activeRole is null
        });
    }

    /// <summary>
    /// Фиксирует активную роль текущей сессии и выпускает токен, ограниченный этой ролью.
    /// </summary>
    [Authorize]
    [HttpPost("session/role")]
    public async Task<IActionResult> SelectRole([FromBody] SelectRoleRequest request, CancellationToken ct)
    {
        var requestedRole = request.Role?.Trim();
        if (string.IsNullOrEmpty(requestedRole))
            return BadRequest(new { message = "Роль обязательна" });

        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (user is null || !user.IsActive)
            return Unauthorized(new { message = "Сессия недействительна. Начните вход заново." });

        var roles = await _userService.GetRolesAsync(user.Id, ct);
        var activeRole = ResolveActiveRole(requestedRole, roles);
        if (activeRole is null)
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "Выбранная роль недоступна для этой учётной записи" });

        var token = _jwt.GenerateToken(user.Id, user.Username, user.Email,
            user.TenantId ?? _db.GetCurrentTenantId(), roles, activeRole);
        _logger.LogInformation(
            "Активная роль {Role} выбрана пользователем {Login} (userId={UserId})",
            activeRole, user.Username, user.Id);

        return Ok(new SelectRoleResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = _jwtSettings.ExpiresMinutes * 60,
            ActiveRole = activeRole,
            Roles = roles
        });
    }

    // ================= 2FA: SETUP =================

    [Authorize]
    [HttpPost("2fa/setup")]
    public async Task<IActionResult> SetupTwoFactor()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user is null) return NotFound();

        if (user.IsTwoFactorEnabled)
            return Conflict(new { message = "2FA уже включена. Сначала отключите её." });

        var secret = _totp.GenerateSecret();
        user.TwoFaSecret = secret;
        await _db.SaveChangesAsync();

        var uri = _totp.BuildOtpAuthUri(Issuer, user.Username, secret);
        var qrBase64 = _totp.GenerateQrCodePngBase64(uri);

        return Ok(new TwoFactorSetupResponse
        {
            Secret = secret,
            OtpAuthUri = uri,
            QrCodeImageBase64 = qrBase64,
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

        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user is null) return NotFound();

        if (user.IsTwoFactorEnabled)
            return Conflict(new { message = "2FA уже включена" });

        if (string.IsNullOrEmpty(user.TwoFaSecret))
            return BadRequest(new { message = "Секрет не сгенерирован. Сначала вызовите /2fa/setup." });

        if (!_totp.VerifyCode(user.TwoFaSecret, request.Code))
            return BadRequest(new { message = "Неверный код. Проверьте время на устройстве." });

        user.IsTwoFactorEnabled = true;
        user.TwoFactorEnabledAt = DateTimeOffset.UtcNow;
        var recoveryCodes = GenerateRecoveryCodes();
        user.RecoveryCodesJson = JsonSerializer.Serialize(recoveryCodes.Select(HashRecoveryCode).ToList());
        await _db.SaveChangesAsync();

        _logger.LogInformation("2FA (TOTP) включена для пользователя {Login}", user.Username);

        return Ok(new { message = "2FA успешно включена", recoveryCodes });
    }

    [Authorize]
    [HttpGet("2fa/status")]
    public async Task<IActionResult> GetTwoFactorStatus()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user is null) return NotFound();
        var roles = await _userService.GetRolesAsync(user.Id);
        var tenant = user.TenantId.HasValue
            ? await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == user.TenantId.Value)
            : null;
        var mode = tenant?.TwoFactorMode ?? "all";
        var isPortalOnly = roles.Count == 1 && roles[0] == "portal_user";
        var isPrivileged = roles.Any(role => role is "admin" or "tenant_owner" or "tenant_admin");
        var isRequired = !isPortalOnly && (mode == "all" || (mode == "admins" && isPrivileged));

        return Ok(new TwoFactorStatusResponse
        {
            IsEnabled = user.IsTwoFactorEnabled,
            EnabledAt = user.TwoFactorEnabledAt,
            IsRequired = isRequired,
            GraceEndsAt = user.TwoFactorGraceEndsAt
        });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            return BadRequest(new { message = "Текущий пароль обязателен" });
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { message = "Новый пароль должен содержать не менее 8 символов" });

        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user is null) return NotFound();

        if (string.IsNullOrEmpty(user.PasswordHash)
            || !_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return Unauthorized(new { message = "Неверный текущий пароль" });

        if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
            return BadRequest(new { message = "Новый пароль должен отличаться от текущего" });

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.PasswordChangedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Пароль изменён пользователем {Login}", user.Username);
        return Ok(new { message = "Пароль успешно изменён" });
    }

    [Authorize]
    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor([FromBody] DisableTwoFactorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Пароль обязателен" });

        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user is null) return NotFound();

        var roles = await _userService.GetRolesAsync(user.Id);
        var tenant = user.TenantId.HasValue
            ? await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == user.TenantId.Value)
            : null;
        var mode = tenant?.TwoFactorMode ?? "all";
        var isPortalOnly = roles.Count == 1 && roles[0] == "portal_user";
        var isPrivileged = roles.Any(role => role is "admin" or "tenant_owner" or "tenant_admin");
        var required = !isPortalOnly && (mode == "all" || (mode == "admins" && isPrivileged));
        if (required)
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "2FA обязательна для этой учётной записи и не может быть отключена."
            });

        // Проверяем пароль через BCrypt
        if (string.IsNullOrEmpty(user.PasswordHash)
            || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Неверный пароль" });
        }

        user.IsTwoFactorEnabled = false;
        user.TwoFaSecret = null;
        user.TwoFactorEnabledAt = null;
        user.RecoveryCodesJson = null;
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

    private string CreateLoginChallenge(int userId, bool isSetup)
    {
        var challengeId = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        _cache.Set(
            GetLoginChallengeKey(challengeId),
            new LoginChallenge(userId, isSetup),
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = LoginChallengeLifetime
            });

        return challengeId;
    }

    private bool TryGetLoginChallenge(string challengeId, out LoginChallenge challenge)
        => _cache.TryGetValue(GetLoginChallengeKey(challengeId), out challenge!);

    private static string GetLoginChallengeKey(string challengeId) => $"auth:login:2fa:{challengeId}";

    private static List<string> GenerateRecoveryCodes()
    {
        var codes = new List<string>(10);
        for (var i = 0; i < 10; i++)
        {
            var bytes = RandomNumberGenerator.GetBytes(5);
            var value = Convert.ToHexString(bytes);
            codes.Add($"{value[..4]}-{value.Substring(4, 4)}-{value.Substring(8, 2)}");
        }
        return codes;
    }

    private static string NormalizeRecoveryCode(string code)
        => code.Replace("-", string.Empty).Trim().ToUpperInvariant();

    private static string HashRecoveryCode(string code)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(NormalizeRecoveryCode(code));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static bool TryConsumeRecoveryCode(Domain.Entities.User user, string code)
    {
        if (string.IsNullOrWhiteSpace(user.RecoveryCodesJson))
            return false;

        var normalized = NormalizeRecoveryCode(code);
        if (normalized.Length != 10)
            return false;

        List<string>? stored;
        try
        {
            stored = JsonSerializer.Deserialize<List<string>>(user.RecoveryCodesJson);
        }
        catch (JsonException)
        {
            return false;
        }

        if (stored is null)
            return false;

        var hash = HashRecoveryCode(normalized);
        var index = stored.FindIndex(value =>
            string.Equals(value, hash, StringComparison.OrdinalIgnoreCase)
            || string.Equals(NormalizeRecoveryCode(value), normalized, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
            return false;

        stored.RemoveAt(index);
        user.RecoveryCodesJson = JsonSerializer.Serialize(stored);
        return true;
    }

    private VerifyTwoFactorResponse CreateTokenResponse(
        itsm.Domain.Entities.User user,
        List<string> roles)
    {
        // Одна роль — активна сразу; несколько — ждём выбора на промежуточном экране.
        var activeRole = roles.Count == 1 ? roles[0] : null;
        var token = _jwt.GenerateToken(user.Id, user.Username, user.Email,
            user.TenantId ?? _db.GetCurrentTenantId(), roles, activeRole);
        _logger.LogInformation("JWT выпущен для {Login} (userId={UserId})", user.Username, user.Id);
        return new VerifyTwoFactorResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = _jwtSettings.ExpiresMinutes * 60
        };
    }

    /// <summary>
    /// Сопоставляет запрошенную/текущую роль со списком ролей из БД (без учёта регистра).
    /// Возвращает null, если роль пользователю не назначена.
    /// </summary>
    private static string? ResolveActiveRole(string? role, List<string> roles)
    {
        if (string.IsNullOrWhiteSpace(role)) return null;
        return roles.FirstOrDefault(candidate =>
            string.Equals(candidate, role.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private sealed record LoginChallenge(int UserId, bool IsSetup);
}
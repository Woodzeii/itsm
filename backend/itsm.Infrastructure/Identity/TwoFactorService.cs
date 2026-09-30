using System.Security.Cryptography;
using System.Text;
using itsm.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace itsm.Infrastructure.Identity;

public class TwoFactorService : ITwoFactorService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;
    private readonly ILogger<TwoFactorService> _logger;

    public TwoFactorService(IMemoryCache cache, ILogger<TwoFactorService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public string GenerateCode(int userId, string login, string email)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var cacheKey = GetCacheKey(userId);

        var payload = new TwoFactorUserData
        {
            UserId = userId,
            Login = login,
            Email = email
        };

        _cache.Set(cacheKey, (Code: code, Data: payload), new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CodeLifetime
        });

        _logger.LogInformation(
            "2FA код для пользователя {UserId} сгенерирован (TTL {Minutes} мин)",
            userId, CodeLifetime.TotalMinutes);

        return code;
    }

    public TwoFactorUserData? ValidateCode(int userId, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var cacheKey = GetCacheKey(userId);

        if (!_cache.TryGetValue<(string Code, TwoFactorUserData Data)>(cacheKey, out var entry))
        {
            _logger.LogWarning("2FA: код для пользователя {UserId} не найден или истёк", userId);
            return null;
        }

        if (string.IsNullOrEmpty(entry.Code))
        {
            _cache.Remove(cacheKey);
            return null;
        }

        var isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(entry.Code),
            Encoding.UTF8.GetBytes(code));

        if (!isValid)
        {
            _logger.LogWarning("2FA: неверный код для пользователя {UserId}", userId);
            return null;
        }

        // Код одноразовый — удаляем после успешной проверки
        _cache.Remove(cacheKey);
        _logger.LogInformation("2FA: код для пользователя {UserId} успешно проверен", userId);

        return entry.Data;
    }

    public void InvalidateCode(int userId)
    {
        _cache.Remove(GetCacheKey(userId));
        _logger.LogInformation("2FA: код для пользователя {UserId} аннулирован", userId);
    }

    private static string GetCacheKey(int userId) => $"2fa:code:{userId}";
}
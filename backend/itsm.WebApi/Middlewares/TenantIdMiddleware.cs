using itsm.Infrastructure.Persistence;
using System.Security.Claims;

namespace itsm.WebApi.Middlewares;

/// <summary>
/// Middleware для установки текущего TenantId из JWT токена или HTTP заголовка.
/// Должно быть добавлено после app.UseAuthentication() и перед app.MapControllers().
/// </summary>
public class TenantIdMiddleware
{
    private readonly RequestDelegate _next;

    public TenantIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ItsmDbContext dbContext,
        ITenantContext tenantContext)
    {
        var tenantId = ExtractTenantId(context);
        
        if (tenantId > 0)
        {
            tenantContext.SetCurrentTenantId(tenantId);
            dbContext.SetTenantId(tenantId);
        }

        await _next(context);
    }

    /// <summary>
    /// Извлекает TenantId из JWT токена или HTTP заголовка.
    /// Приоритет:
    /// 1. JWT claim "tenant_id"
    /// 2. HTTP заголовок "X-Tenant-Id"
    /// 3. Значение по умолчанию (1)
    /// </summary>
    private static int ExtractTenantId(HttpContext context)
    {
        // Пытаемся получить из JWT claims (установлено после аутентификации)
        if (context.User?.FindFirst("tenant_id") is not null)
        {
            if (int.TryParse(context.User.FindFirst("tenant_id")?.Value, out var tenantIdFromClaim))
            {
                return tenantIdFromClaim;
            }
        }

        // Пытаемся получить из HTTP заголовка (для тестирования или обслуживания)
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantIdHeader))
        {
            if (int.TryParse(tenantIdHeader.ToString(), out var tenantIdFromHeader))
            {
                return tenantIdFromHeader;
            }
        }

        // Значение по умолчанию
        return 1;
    }
}

/// <summary>
/// Extension методы для регистрации TenantIdMiddleware.
/// </summary>
public static class TenantIdMiddlewareExtensions
{
    /// <summary>
    /// Добавляет TenantIdMiddleware в pipeline.
    /// Должно быть вызвано после app.UseAuthentication() и перед app.MapControllers().
    /// </summary>
    public static IApplicationBuilder UseTenantIdMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantIdMiddleware>();
    }
}

using FluentValidation;
using itsm.Application.Common.Interfaces;
using itsm.Infrastructure.Persistence.Services;
using itsm.Application.Common.Models;
using itsm.Application.Common.Validators;
using itsm.Infrastructure.Identity;
using itsm.Infrastructure.Notifications;
using itsm.Infrastructure.Persistence;
using itsm.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace itsm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = DatabaseConfiguration.GetConnectionString(key => configuration[key]);

        // Регистрируем TenantContext для поддержки мультитенантности
        services.AddScoped<ITenantContext, TenantContext>();

        services.AddDbContext<ItsmDbContext>((provider, options) =>
        {
            var tenantContext = provider.GetRequiredService<ITenantContext>();
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
        });

        services.AddMemoryCache();
        services.AddHttpContextAccessor();

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<ITwoFactorService, TwoFactorService>();
        services.AddSingleton<ITotpService, TotpService>();

        // Auth / Registration
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IEmailService, LoggingEmailService>();
        services.AddScoped<IUserService, UserService>();
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IDictionaryService, DictionaryService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IAssetClassService, AssetClassService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<ITicketAttachmentStorage, PhysicalTicketAttachmentStorage>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

        return services;
    }
}
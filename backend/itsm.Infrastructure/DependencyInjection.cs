using FluentValidation;
using itsm.Application.Common.Interfaces;
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

        services.AddDbContext<ItsmDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.AddMemoryCache();

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<ITwoFactorService, TwoFactorService>();
        services.AddScoped<ILdapService, MockLdapService>();

        // Auth / Registration
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IEmailService, LoggingEmailService>();
        services.AddScoped<IUserService, UserService>();
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

        return services;
    }
}
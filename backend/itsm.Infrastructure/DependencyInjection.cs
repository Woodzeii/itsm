using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Infrastructure.Identity;
using itsm.Infrastructure.Persistence;
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

        services.AddScoped<ILdapService, MockLdapService>();
        services.AddSingleton<ITwoFactorService, TwoFactorService>();

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddSingleton<IJwtService, JwtService>();

        return services;
    }
}
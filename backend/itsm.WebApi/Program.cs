using System.Text;
using Scalar.AspNetCore;
using itsm.Application.Common.Interfaces;
using itsm.Infrastructure;
using itsm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

DotEnvLoader.LoadIfPresent();
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Запуск ITSM API");

    var builder = WebApplication.CreateBuilder(args);

    if (!builder.Environment.IsEnvironment("Testing"))
    {
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());
    }

    builder.Services.AddOpenApi();
    builder.Services.AddControllers();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DevCors", policy =>
            policy.WithOrigins("http://localhost:4200")
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    });
    builder.Services.AddInfrastructure(builder.Configuration);

    var jwtSection = builder.Configuration.GetSection("Jwt");
    var jwtKey = jwtSection["Key"]
        ?? throw new InvalidOperationException("Jwt:Key не настроен. Проверьте .env (Jwt__Key).");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSection["Issuer"],
                ValidAudience = jwtSection["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services.AddAuthorization();

    var app = builder.Build();
    app.UseCors("DevCors");

    if (!app.Environment.IsEnvironment("Testing"))
    {
        app.UseSerilogRequestLogging();
    }

    // ===== Автомиграции: применяем все pending-миграции при старте =====
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();
        db.Database.Migrate();
        Log.Information("Автомиграции: применены все pending-миграции");
    }

    // Инициализация тестовых пользователей в Development
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        seeder.SeedAsync().GetAwaiter().GetResult();
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Приложение упало с фатальной ошибкой");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
using itsm.Application.Common.Interfaces;
using itsm.Infrastructure.Persistence;
using itsm.Tests.Mocks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Testcontainers.PostgreSql;

namespace itsm.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ValidCode = "123456";
    public const string TestJwtKey = "integration-tests-secret-key-at-least-32-chars-long!!";

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("itsm_tests")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public Mock<ITwoFactorService> TwoFactorMock { get; } = new();
    public RecordingEmailService EmailService { get; } = new();

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ItsmDbContext>>();
            services.RemoveAll<ItsmDbContext>();

            services.AddDbContext<ItsmDbContext>(options =>
                options.UseNpgsql(_dbContainer.GetConnectionString())
                    .UseSnakeCaseNamingConvention());

            TwoFactorMock
                .Setup(x => x.GenerateCode(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(ValidCode);

            TwoFactorMock
                .Setup(x => x.ValidateCode(It.IsAny<int>(), ValidCode))
                .Returns((int userId, string _) => new TwoFactorUserData
                {
                    UserId = userId,
                    Login = "admin",
                    Email = "admin@itsm.local"
                });

            TwoFactorMock
                .Setup(x => x.ValidateCode(It.IsAny<int>(), It.Is<string>(c => c != ValidCode)))
                .Returns((TwoFactorUserData?)null);

            TwoFactorMock
                .Setup(x => x.InvalidateCode(It.IsAny<int>()));

            services.RemoveAll<ITwoFactorService>();
            services.AddSingleton(TwoFactorMock.Object);

            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(EmailService);

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();
            db.Database.Migrate();
        });
    }
}
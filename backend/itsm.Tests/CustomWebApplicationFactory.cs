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

    public Mock<ITotpService> TotpMock { get; } = new();
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

            TotpMock
                .Setup(x => x.GenerateSecret())
                .Returns("JBSWY3DPEHPK3PXP");
            TotpMock
                .Setup(x => x.VerifyCode(It.IsAny<string>(), ValidCode))
                .Returns(true);
            TotpMock
                .Setup(x => x.VerifyCode(It.IsAny<string>(), It.Is<string>(code => code != ValidCode)))
                .Returns(false);
            TotpMock
                .Setup(x => x.BuildOtpAuthUri(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns("otpauth://totp/ITSM%20NC:test?secret=JBSWY3DPEHPK3PXP&issuer=ITSM%20NC&digits=6&period=30");
            TotpMock
                .Setup(x => x.GenerateQrCodePngBase64(It.IsAny<string>(), It.IsAny<int>()))
                .Returns("mock-qr-code");

            services.RemoveAll<ITotpService>();
            services.AddSingleton(TotpMock.Object);

            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(EmailService);

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();
            db.Database.Migrate();

            var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
            seeder.SeedAsync().GetAwaiter().GetResult();
        });
    }
}
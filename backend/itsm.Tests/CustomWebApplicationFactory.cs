using itsm.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace itsm.Tests;
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string ValidCode = "123456";
    public const string TestJwtKey = "integration-tests-secret-key-at-least-32-chars-long!!";

    public Mock<ITwoFactorService> TwoFactorMock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("Testing");

		// Тестовый Jwt:Key
		builder.ConfigureAppConfiguration((_, config) =>
		{
			config.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Jwt:Key"] = TestJwtKey,
				["Jwt:Issuer"] = "itsm-api",
				["Jwt:Audience"] = "itsm-client",
				["Jwt:ExpiresMinutes"] = "60"
			});
		});

		// ВАЖНО: ConfigureTestServices выполняется ПОСЛЕ Program.cs,
		// поэтому RemoveAll действительно уберёт реальный TwoFactorService.
		builder.ConfigureTestServices(services =>
		{
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
		});
	}
}
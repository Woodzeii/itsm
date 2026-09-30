using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Moq;

namespace itsm.Tests;

[Collection(IntegrationTestCollection.Name)]
public class AuthTests
{
    private readonly CustomWebApplicationFactory _factory;

    static AuthTests()
    {
        // Program.cs читает Jwt:Key до того, как WebApplicationFactory подменит конфиг.
        // Задаём через env-переменные — WebApplication.CreateBuilder их подхватит.
        Environment.SetEnvironmentVariable("Jwt__Key", CustomWebApplicationFactory.TestJwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "itsm-api");
        Environment.SetEnvironmentVariable("Jwt__Audience", "itsm-client");
        Environment.SetEnvironmentVariable("Jwt__ExpiresMinutes", "60");

        // Заглушки для строки подключения — реально не используются,
        // потому что DbContext подменяется фабрикой на Testcontainers.
        Environment.SetEnvironmentVariable("POSTGRES_DB", "itsm_tests");
        Environment.SetEnvironmentVariable("POSTGRES_USER", "test");
        Environment.SetEnvironmentVariable("POSTGRES_PASSWORD", "test");
        Environment.SetEnvironmentVariable("POSTGRES_HOST", "localhost");
        Environment.SetEnvironmentVariable("POSTGRES_PORT", "5432");
    }

    public AuthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        ResetTwoFactorMock();
    }

    private void ResetTwoFactorMock()
    {
        _factory.TwoFactorMock.Reset();

        _factory.TwoFactorMock
            .Setup(x => x.GenerateCode(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(CustomWebApplicationFactory.ValidCode);

        _factory.TwoFactorMock
            .Setup(x => x.ValidateCode(It.IsAny<int>(), CustomWebApplicationFactory.ValidCode))
            .Returns((int userId, string _) => new TwoFactorUserData
            {
                UserId = userId,
                Login = "admin",
                Email = "admin@itsm.local"
            });

        _factory.TwoFactorMock
            .Setup(x => x.ValidateCode(It.IsAny<int>(), It.Is<string>(c => c != CustomWebApplicationFactory.ValidCode)))
            .Returns((TwoFactorUserData?)null);

        _factory.TwoFactorMock
            .Setup(x => x.InvalidateCode(It.IsAny<int>()));
    }

    // ============ AUTH-QA.2: успешный сценарий ============
    [Fact]
    public async Task Login_Then_Verify2Fa_ReturnsJwt()
    {
        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "admin123" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();
        login!.RequiresTwoFactor.Should().BeTrue();
        login.UserId.Should().BeGreaterThan(0);

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-2fa",
            new VerifyTwoFactorRequest
            {
                UserId = login.UserId,
                Code = CustomWebApplicationFactory.ValidCode
            });
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var verify = await verifyResponse.Content.ReadFromJsonAsync<VerifyTwoFactorResponse>();
        verify.Should().NotBeNull();
        verify!.AccessToken.Should().NotBeNullOrWhiteSpace();
        verify.TokenType.Should().Be("Bearer");
        verify.ExpiresIn.Should().BeGreaterThan(0);
    }

    // ============ AUTH-QA.3 ============
    [Fact]
    public async Task Login_WithWrongPassword_Returns401_AndNoSession()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "wrong-password" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadAsStringAsync() ?? string.Empty;
        body.Should().NotContain("eyJ");
    }

    // ============ AUTH-QA.4 ============
    [Fact]
    public async Task Verify2Fa_WithWrongCode_Returns401_AndNoJwt()
    {
        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "admin123" });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-2fa",
            new VerifyTwoFactorRequest { UserId = login!.UserId, Code = "000000" });

        verifyResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await verifyResponse.Content.ReadAsStringAsync() ?? string.Empty;
        body.Should().NotContain("accessToken");
        body.Should().NotContain("eyJ");
    }

    // ============ AUTH-QA.5 ============
    [Fact]
    public async Task Verify2Fa_WithExpiredCode_Returns401()
    {
        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "admin123" });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();

        _factory.TwoFactorMock
            .Setup(x => x.ValidateCode(login!.UserId, It.IsAny<string>()))
            .Returns((TwoFactorUserData?)null);

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-2fa",
            new VerifyTwoFactorRequest
            {
                UserId = login.UserId,
                Code = CustomWebApplicationFactory.ValidCode
            });

        verifyResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ============ AUTH-QA.6 ============
    [Fact]
    public async Task Verify2Fa_ReturnsJwtWithRequiredClaims()
    {
        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "admin123" });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-2fa",
            new VerifyTwoFactorRequest
            {
                UserId = login!.UserId,
                Code = CustomWebApplicationFactory.ValidCode
            });
        var verify = await verifyResponse.Content.ReadFromJsonAsync<VerifyTwoFactorResponse>();
        verify.Should().NotBeNull();

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(verify!.AccessToken);

        token.Claims.Should().Contain(c => c.Type == "Id" && c.Value == login.UserId.ToString());
        token.Claims.Should().Contain(c => c.Type == "Login" && c.Value == "admin");
        token.Claims.Should().Contain(c => c.Type == "Email" && c.Value == "admin@itsm.local");
        token.Claims.Should().Contain(c => c.Type == "iss" && c.Value == "itsm-api");
        token.Claims.Should().Contain(c => c.Type == "aud" && c.Value == "itsm-client");
    }
}
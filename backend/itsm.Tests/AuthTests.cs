using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using itsm.Application.Common.Models;
using itsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
    }

    // ============ AUTH-QA.2: успешный сценарий ============
    [Fact]
    public async Task Login_Then_Verify2Fa_ReturnsJwt()
    {
        await ResetTotpAsync("admin");
        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "admin123" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();
        login!.RequiresTwoFactor.Should().BeTrue();
        login.RequiresTwoFactorSetup.Should().BeTrue();
        login.CanSkipTwoFactorSetup.Should().BeFalse();
        login.QrCodeImageBase64.Should().NotBeNullOrWhiteSpace();
        login.UserId.Should().BeGreaterThan(0);

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-2fa",
            new VerifyTwoFactorRequest
            {
                ChallengeId = login.ChallengeId,
                Code = CustomWebApplicationFactory.ValidCode
            });
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var verify = await verifyResponse.Content.ReadFromJsonAsync<VerifyTwoFactorResponse>();
        verify.Should().NotBeNull();
        verify!.AccessToken.Should().NotBeNullOrWhiteSpace();
        verify.TokenType.Should().Be("Bearer");
        verify.ExpiresIn.Should().BeGreaterThan(0);
        (await IsTotpEnabledAsync("admin")).Should().BeTrue();

        var repeatLoginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "admin123" });
        var repeatLogin = await repeatLoginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        repeatLogin.Should().NotBeNull();
        repeatLogin!.RequiresTwoFactorSetup.Should().BeFalse();
        repeatLogin.QrCodeImageBase64.Should().BeEmpty();

        var repeatVerifyResponse = await client.PostAsJsonAsync("/api/auth/verify-2fa",
            new VerifyTwoFactorRequest
            {
                ChallengeId = repeatLogin.ChallengeId,
                Code = CustomWebApplicationFactory.ValidCode
            });
        repeatVerifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
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
            new VerifyTwoFactorRequest { ChallengeId = login!.ChallengeId, Code = "000000" });

        verifyResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await verifyResponse.Content.ReadAsStringAsync() ?? string.Empty;
        body.Should().NotContain("accessToken");
        body.Should().NotContain("eyJ");
    }

    // ============ AUTH-QA.5 ============
    [Fact]
    public async Task Verify2Fa_WithUnknownChallenge_Returns401()
    {
        var client = _factory.CreateClient();

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-2fa",
            new VerifyTwoFactorRequest
            {
                ChallengeId = "unknown-challenge",
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
                ChallengeId = login!.ChallengeId,
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
        
        // Проверяем наличие Role claims для ASP.NET [Authorize(Roles = "")] атрибутов
        token.Claims.Should().Contain(c => c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
        
        // Проверяем наличие JSON-массива ролей в custom claim "roles" для фронтенда
        var rolesClaim = token.Claims.FirstOrDefault(c => c.Type == "roles");
        rolesClaim.Should().NotBeNull("Custom claim 'roles' должен содержать JSON-массив ролей");
        rolesClaim!.Value.Should().StartWith("[").And.EndWith("]");
    }

    // ============ AUTH-QA.7 ============
    [Fact]
    public async Task Verify2Fa_ReturnsJwtWithRolesAsJsonArray()
    {
        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "admin123" });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-2fa",
            new VerifyTwoFactorRequest
            {
                ChallengeId = login!.ChallengeId,
                Code = CustomWebApplicationFactory.ValidCode
            });
        var verify = await verifyResponse.Content.ReadFromJsonAsync<VerifyTwoFactorResponse>();
        verify.Should().NotBeNull();

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(verify!.AccessToken);

        // Парсим JSON-массив ролей из custom claim
        var rolesClaim = token.Claims.FirstOrDefault(c => c.Type == "roles");
        rolesClaim.Should().NotBeNull();

        var rolesJson = rolesClaim!.Value;
        var roles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(rolesJson);
        
        roles.Should().NotBeEmpty();
        roles.Should().Contain("admin");
    }

    [Fact]
    public async Task PortalUser_CanSkipSetup_ButRequiredUserCannot()
    {
        var client = _factory.CreateClient();
        var portalLoginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "s.sidorov", Password = "pass123" });
        var portalLogin = await portalLoginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        portalLogin.Should().NotBeNull();
        portalLogin!.RequiresTwoFactorSetup.Should().BeTrue();
        portalLogin.CanSkipTwoFactorSetup.Should().BeTrue();
        portalLogin.QrCodeImageBase64.Should().NotBeNullOrWhiteSpace();

        var skipResponse = await client.PostAsJsonAsync("/api/auth/skip-2fa-setup",
            new SkipTwoFactorSetupRequest { ChallengeId = portalLogin.ChallengeId });
        skipResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await IsTotpEnabledAsync("s.sidorov")).Should().BeFalse();

        await ResetTotpAsync("admin");
        var adminLoginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "admin123" });
        var adminLogin = await adminLoginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var deniedSkipResponse = await client.PostAsJsonAsync("/api/auth/skip-2fa-setup",
            new SkipTwoFactorSetupRequest { ChallengeId = adminLogin!.ChallengeId });
        deniedSkipResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task ResetTotpAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();
        var user = await db.Users.SingleAsync(u => u.Username == username);
        user.IsTwoFactorEnabled = false;
        user.TwoFaSecret = null;
        user.TwoFactorEnabledAt = null;
        await db.SaveChangesAsync();
    }

    private async Task<bool> IsTotpEnabledAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();
        return await db.Users
            .Where(u => u.Username == username)
            .Select(u => u.IsTwoFactorEnabled)
            .SingleAsync();
    }
}
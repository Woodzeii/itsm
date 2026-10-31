using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using itsm.Application.Common.Models;
using itsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace itsm.Tests;

/// <summary>
/// Выбор активной роли сессии после успешной 2FA (мультиролевые пользователи).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class AuthSessionTests
{
    private const string MultiRoleUser = "m.multi";
    private const string MultiRolePassword = "pass123";

    private readonly CustomWebApplicationFactory _factory;

    static AuthSessionTests()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", CustomWebApplicationFactory.TestJwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "itsm-api");
        Environment.SetEnvironmentVariable("Jwt__Audience", "itsm-client");
        Environment.SetEnvironmentVariable("Jwt__ExpiresMinutes", "60");

        Environment.SetEnvironmentVariable("POSTGRES_DB", "itsm_tests");
        Environment.SetEnvironmentVariable("POSTGRES_USER", "test");
        Environment.SetEnvironmentVariable("POSTGRES_PASSWORD", "test");
        Environment.SetEnvironmentVariable("POSTGRES_HOST", "localhost");
        Environment.SetEnvironmentVariable("POSTGRES_PORT", "5432");
    }

    public AuthSessionTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Session_ForMultiRoleUser_RequiresRoleSelectionAndHasNoRoleClaims()
    {
        await ResetTotpAsync(MultiRoleUser);
        var client = _factory.CreateClient();

        var token = await LoginAsync(client, MultiRoleUser, MultiRolePassword);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().NotContain(claim => claim.Type == ClaimTypes.Role,
            "до выбора активной роли мультиролевой токен не должен открывать role-protected API");
        jwt.Claims.Should().NotContain(claim => claim.Type == "activeRole");

        ReadRolesClaim(jwt).Should().BeEquivalentTo(new[] { "admin", "agent" });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/auth/session");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        session.Should().NotBeNull();
        session!.UserId.Should().BeGreaterThan(0);
        session.Login.Should().Be(MultiRoleUser);
        session.Roles.Should().BeEquivalentTo(new[] { "admin", "agent" });
        session.ActiveRole.Should().BeNull();
        session.RequiresRoleSelection.Should().BeTrue();

        // До выбора роли админский API закрыт.
        (await client.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SelectRole_IssuesTokenScopedToSelectedRole()
    {
        await ResetTotpAsync(MultiRoleUser);
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, MultiRoleUser, MultiRolePassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var selectResponse = await client.PostAsJsonAsync(
            "/api/auth/session/role",
            new SelectRoleRequest { Role = "agent" });
        selectResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var selected = await selectResponse.Content.ReadFromJsonAsync<SelectRoleResponse>();
        selected.Should().NotBeNull();
        selected!.ActiveRole.Should().Be("agent");
        selected.Roles.Should().BeEquivalentTo(new[] { "admin", "agent" });
        selected.AccessToken.Should().NotBeNullOrWhiteSpace();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(selected.AccessToken);
        jwt.Claims.Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Should().BeEquivalentTo(new[] { "agent" });
        jwt.Claims.Should().Contain(claim => claim.Type == "activeRole" && claim.Value == "agent");
        ReadRolesClaim(jwt).Should().BeEquivalentTo(new[] { "admin", "agent" });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", selected.AccessToken);
        var sessionResponse = await client.GetAsync("/api/auth/session");
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
        session.Should().NotBeNull();
        session!.ActiveRole.Should().Be("agent");
        session.RequiresRoleSelection.Should().BeFalse();

        // Активная роль agent не даёт доступа к админскому API.
        (await client.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Session_ForSingleRoleUser_IsActiveImmediately()
    {
        await ResetTotpAsync("admin");
        var client = _factory.CreateClient();

        var token = await LoginAsync(client, "admin", "admin123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/auth/session");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        session.Should().NotBeNull();
        session!.Roles.Should().BeEquivalentTo(new[] { "admin" });
        session.ActiveRole.Should().Be("admin");
        session.RequiresRoleSelection.Should().BeFalse();
    }

    [Fact]
    public async Task SelectRole_WithRoleNotAssignedToUser_ReturnsBadRequest()
    {
        await ResetTotpAsync(MultiRoleUser);
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, MultiRoleUser, MultiRolePassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync(
            "/api/auth/session/role",
            new SelectRoleRequest { Role = "tenant_admin" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SessionEndpoints_RequireAuthentication()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/auth/session")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/auth/session/role", new SelectRoleRequest { Role = "admin" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Username = username, Password = password });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();

        var verifyResponse = await client.PostAsJsonAsync(
            "/api/auth/verify-2fa",
            new VerifyTwoFactorRequest
            {
                ChallengeId = login!.ChallengeId,
                Code = CustomWebApplicationFactory.ValidCode
            });
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var verify = await verifyResponse.Content.ReadFromJsonAsync<VerifyTwoFactorResponse>();
        verify.Should().NotBeNull();
        return verify!.AccessToken;
    }

    private static List<string> ReadRolesClaim(JwtSecurityToken jwt)
    {
        var claim = jwt.Claims.FirstOrDefault(c => c.Type == "roles");
        claim.Should().NotBeNull();
        return JsonSerializer.Deserialize<List<string>>(claim!.Value) ?? new List<string>();
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
}

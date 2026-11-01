using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using itsm.Application.Common.Models;
using itsm.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace itsm.Tests;

/// <summary>
/// Проверяет контракт JWT для выбора активной роли без обращения к БД.
/// </summary>
public class JwtServiceTests
{
    private static readonly JwtService Service = new(Options.Create(new JwtSettings
    {
        Key = "unit-tests-secret-key-at-least-32-chars-long!!",
        Issuer = "itsm-api",
        Audience = "itsm-client",
        ExpiresMinutes = 60
    }));

    [Fact]
    public void MultiRoleToken_WithoutActiveRole_HasNoRoleClaims()
    {
        var token = Service.GenerateToken(
            7, "m.multi", "m.multi@itsm.local", new[] { "admin", "agent" });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Claims.Should().NotContain(claim => claim.Type == ClaimTypes.Role);
        jwt.Claims.Should().NotContain(claim => claim.Type == "activeRole");
        ReadRoles(jwt).Should().BeEquivalentTo(new[] { "admin", "agent" });
    }

    [Fact]
    public void MultiRoleToken_WithActiveRole_HasOnlySelectedRoleClaim()
    {
        var token = Service.GenerateToken(
            7, "m.multi", "m.multi@itsm.local", new[] { "admin", "agent" }, "agent");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Claims.Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Should().BeEquivalentTo(new[] { "agent" });
        jwt.Claims.Should().Contain(claim => claim.Type == "activeRole" && claim.Value == "agent");
        ReadRoles(jwt).Should().BeEquivalentTo(new[] { "admin", "agent" });
    }

    [Fact]
    public void SingleRoleToken_IsActiveAutomatically()
    {
        var token = Service.GenerateToken(
            3, "p.petrov", "p.petrov@itsm.local", new[] { "agent" });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Claims.Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Should().BeEquivalentTo(new[] { "agent" });
        jwt.Claims.Should().Contain(claim => claim.Type == "activeRole" && claim.Value == "agent");
    }

    [Fact]
    public void ActiveRole_NotAssignedToUser_IsIgnored()
    {
        var token = Service.GenerateToken(
            7, "m.multi", "m.multi@itsm.local", new[] { "admin", "agent" }, "tenant_admin");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Claims.Should().NotContain(claim => claim.Type == ClaimTypes.Role);
        jwt.Claims.Should().NotContain(claim => claim.Type == "activeRole");
    }

    private static List<string> ReadRoles(JwtSecurityToken jwt)
    {
        var roles = jwt.Claims.Single(claim => claim.Type == "roles").Value;
        return JsonSerializer.Deserialize<List<string>>(roles) ?? new List<string>();
    }
}

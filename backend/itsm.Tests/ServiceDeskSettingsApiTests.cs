using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace itsm.Tests;

[Collection(IntegrationTestCollection.Name)]
public class ServiceDeskSettingsApiTests
{
    private readonly CustomWebApplicationFactory _factory;

    static ServiceDeskSettingsApiTests()
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

    public ServiceDeskSettingsApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdminCannotChangeAdminCreationPolicyButOwnerCan()
    {
        var owner = CreateClient("tenant.owner", "tenant_owner");
        var admin = CreateClient("admin", "admin");

        try
        {
            var ownerResponse = await owner.PutAsJsonAsync("/api/service-desk-settings", new
            {
                allowAdminsCreateAdmins = false
            });
            ownerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            (await ownerResponse.Content.ReadFromJsonAsync<ServiceDeskSettingsDto>())!
                .AllowAdminsCreateAdmins.Should().BeFalse();

            var adminResponse = await admin.PutAsJsonAsync("/api/service-desk-settings", new
            {
                allowAdminsCreateAdmins = true
            });
            adminResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            await owner.PutAsJsonAsync("/api/service-desk-settings", new { allowAdminsCreateAdmins = true });
        }
    }

    [Fact]
    public async Task PasswordPolicyMinimumLengthBelowEightIsRejected()
    {
        var owner = CreateClient("tenant.owner", "tenant_owner");

        var invalid = await owner.PutAsJsonAsync("/api/security-settings", new
        {
            minLength = 7,
            requireDigit = true,
            requireUppercase = false,
            requireLowercase = true,
            requireSpecial = false,
            expirationDays = (int?)null,
            maxFailedAttempts = 5,
            lockoutMinutes = 15
        });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var valid = await owner.PutAsJsonAsync("/api/security-settings", new
        {
            minLength = 8,
            requireDigit = true,
            requireUppercase = false,
            requireLowercase = true,
            requireSpecial = false,
            expirationDays = (int?)null,
            maxFailedAttempts = 5,
            lockoutMinutes = 15
        });
        valid.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CriticalityLimitIsEnforcedAtFiveLevels()
    {
        var admin = CreateClient("admin", "admin");
        var createdIds = new List<int>();

        try
        {
            var existing = await admin.GetFromJsonAsync<List<CriticalityLevelDto>>("/api/criticality-levels");
            existing.Should().NotBeNull();
            var needed = Math.Max(0, 5 - existing!.Count(level => level.IsActive));
            for (var index = 0; index < needed; index++)
            {
                var response = await admin.PostAsJsonAsync("/api/criticality-levels", new
                {
                    code = $"test_{Guid.NewGuid():N}"[..16],
                    name = $"Тестовый уровень {Guid.NewGuid():N}"[..24],
                    sortOrder = 100 + index
                });
                response.StatusCode.Should().Be(HttpStatusCode.Created);
                var created = await response.Content.ReadFromJsonAsync<CriticalityLevelDto>();
                createdIds.Add(created!.Id);
            }

            var overflow = await admin.PostAsJsonAsync("/api/criticality-levels", new
            {
                code = $"overflow_{Guid.NewGuid():N}"[..20],
                name = $"Лишний уровень {Guid.NewGuid():N}"[..24],
                sortOrder = 200
            });
            overflow.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var id in createdIds)
                await admin.DeleteAsync($"/api/criticality-levels/{id}");
        }
    }

    [Fact]
    public async Task TicketTypeCanBeCreatedRenamedAndDeleted()
    {
        var admin = CreateClient("admin", "admin");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        int? typeId = null;

        try
        {
            var created = await admin.PostAsJsonAsync("/api/ticket-types", new
            {
                code = $"api_test_{suffix}",
                name = $"API тест {suffix}",
                isPortalAvailable = true,
                isSlaEnabled = true
            });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var type = await created.Content.ReadFromJsonAsync<TicketTypeDto>();
            typeId = type!.Id;

            var renamed = await admin.PutAsJsonAsync($"/api/ticket-types/{typeId}", new
            {
                name = $"API тест обновлён {suffix}",
                isPortalAvailable = false,
                isSlaEnabled = false
            });
            renamed.StatusCode.Should().Be(HttpStatusCode.OK);

            var deleted = await admin.DeleteAsync($"/api/ticket-types/{typeId}");
            deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
            typeId = null;
        }
        finally
        {
            if (typeId is int id)
                await admin.DeleteAsync($"/api/ticket-types/{id}");
        }
    }

    private HttpClient CreateClient(string username, params string[] roles)
    {
        var client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();

        var user = db.Users.IgnoreQueryFilters().First(item => item.Username == username);
        var token = jwt.GenerateToken(user.Id, user.Username, user.Email, user.TenantId ?? 1, roles);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
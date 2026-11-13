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
public class TicketAssignmentApiTests
{
    private readonly CustomWebApplicationFactory _factory;

    static TicketAssignmentApiTests()
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

    public TicketAssignmentApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AssignmentSettingIsReadableByAgentsAndEditableByAdminsOnly()
    {
        var adminClient = CreateClient("admin", "admin");
        var agentClient = CreateClient("p.petrov", "agent");

        var initial = await agentClient.GetFromJsonAsync<AssignmentSettingResponse>("/api/tickets/assignment-setting");
        initial.Should().NotBeNull();
        initial!.Mode.Should().BeOneOf("auto", "manual");

        var updated = await adminClient.PutAsJsonAsync("/api/tickets/assignment-setting", new { mode = "manual" });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await updated.Content.ReadFromJsonAsync<AssignmentSettingResponse>())!.Mode.Should().Be("manual");

        var forbidden = await agentClient.PutAsJsonAsync("/api/tickets/assignment-setting", new { mode = "auto" });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var invalid = await adminClient.PutAsJsonAsync("/api/tickets/assignment-setting", new { mode = "sometimes" });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatedTicketIsAutoAssignedInAutoMode()
    {
        var adminClient = CreateClient("admin", "admin");
        await SetModeAsync(adminClient, "auto");
        var creatorClient = CreateClient("m.maria", "portal_user");

        var created = await creatorClient.PostAsJsonAsync("/api/tickets", new
        {
            title = $"Автораспределение {Guid.NewGuid():N}",
            description = "Проверка автораспределения",
            typeId = 1,
            priority = "Средний"
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticket = await created.Content.ReadFromJsonAsync<TicketResponse>();

        ticket!.AssigneeId.Should().NotBeNull();
        ticket.AssigneeName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreatedTicketStaysUnassignedInManualMode()
    {
        var adminClient = CreateClient("admin", "admin");
        await SetModeAsync(adminClient, "manual");
        var creatorClient = CreateClient("m.maria", "portal_user");

        var created = await creatorClient.PostAsJsonAsync("/api/tickets", new
        {
            title = $"Ручной режим {Guid.NewGuid():N}",
            description = "Проверка ручного режима",
            typeId = 1,
            priority = "Средний"
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        (await created.Content.ReadFromJsonAsync<TicketResponse>())!.AssigneeId.Should().BeNull();
    }

    [Fact]
    public async Task ManualAssignmentAssignsRequestedAgent()
    {
        var adminClient = CreateClient("admin", "admin");
        await SetModeAsync(adminClient, "manual");
        var creatorClient = CreateClient("m.maria", "portal_user");
        var agentId = await GetUserIdAsync("p.petrov");
        var ticketId = await CreateTicketAsync(creatorClient);

        var response = await adminClient.PostAsJsonAsync($"/api/tickets/{ticketId}/assign", new { assigneeId = agentId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<TicketResponse>())!.AssigneeId.Should().Be(agentId);
    }

    [Fact]
    public async Task ManualAssignmentToNonAgentIsRejected()
    {
        var adminClient = CreateClient("admin", "admin");
        var creatorClient = CreateClient("m.maria", "portal_user");
        var portalUserId = await GetUserIdAsync("s.sidorov");
        var ticketId = await CreateTicketAsync(creatorClient);

        var response = await adminClient.PostAsJsonAsync($"/api/tickets/{ticketId}/assign", new { assigneeId = portalUserId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ForceAutoAssignPicksAgentAndAssignsTicket()
    {
        var adminClient = CreateClient("admin", "admin");
        await SetModeAsync(adminClient, "manual");
        var creatorClient = CreateClient("m.maria", "portal_user");
        var ticketId = await CreateTicketAsync(creatorClient);

        var response = await adminClient.PostAsync($"/api/tickets/{ticketId}/auto-assign", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AutoAssignResponse>();
        result!.TicketId.Should().Be(ticketId);
        result.AssigneeId.Should().BeGreaterThan(0);

        var ticket = await adminClient.GetFromJsonAsync<TicketResponse>($"/api/tickets/{ticketId}");
        ticket!.AssigneeId.Should().Be(result.AssigneeId);
    }

    [Fact]
    public async Task AutoAssignForUnknownTicketReturnsNotFound()
    {
        var adminClient = CreateClient("admin", "admin");

        var response = await adminClient.PostAsync("/api/tickets/999999/auto-assign", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private HttpClient CreateClient(string username, params string[] roles)
    {
        var client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();

        var user = db.Users.IgnoreQueryFilters().Include(item => item.RoleMappings).First(item => item.Username == username);
        var token = jwt.GenerateToken(user.Id, user.Username, user.Email, user.TenantId ?? 1, roles);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<int> GetUserIdAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();
        return await db.Users.IgnoreQueryFilters().Where(user => user.Username == username).Select(user => user.Id).FirstAsync();
    }

    private static async Task SetModeAsync(HttpClient adminClient, string mode)
    {
        var response = await adminClient.PutAsJsonAsync("/api/tickets/assignment-setting", new { mode });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<int> CreateTicketAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = $"Назначение {Guid.NewGuid():N}",
            description = "Заявка для проверки назначения",
            typeId = 1,
            priority = "Средний"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<TicketResponse>())!.Id;
    }
}

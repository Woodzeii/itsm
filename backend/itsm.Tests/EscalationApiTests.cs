using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace itsm.Tests;

[Collection(IntegrationTestCollection.Name)]
public class EscalationApiTests
{
    private readonly CustomWebApplicationFactory _factory;

    static EscalationApiTests()
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

    public EscalationApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Escalate_Ticket_MovesToEscalatedStatusAndNotifiesHead()
    {
        var (headId, headClient) = await CreateAgentClientAsync("p.petrov");
        await SetServiceDeskHeadAsync(headId);
        var creatorClient = await CreatePortalClientAsync("m.maria");
        var ticketId = await CreateTicketAsync(creatorClient);

        var response = await creatorClient.PostAsync($"/api/tickets/{ticketId}/escalate", null);

        var payload = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        var escalation = await response.Content.ReadFromJsonAsync<TicketEscalationResponse>();
        escalation!.TicketId.Should().Be(ticketId);
        escalation.AcceptedAt.Should().BeNull();

        var ticket = await creatorClient.GetFromJsonAsync<TicketResponse>($"/api/tickets/{ticketId}");
        ticket!.StatusName.Should().Be("Эскалирована");

        var pending = await headClient.GetFromJsonAsync<List<PendingEscalationResponse>>("/api/escalations");
        pending!.Should().Contain(item => item.TicketId == ticketId);
    }

    [Fact]
    public async Task Accept_Escalation_ReturnsInProgressTicketAssignedToAccepter()
    {
        var (headId, headClient) = await CreateAgentClientAsync("p.petrov");
        await SetServiceDeskHeadAsync(headId);
        var creatorClient = await CreatePortalClientAsync("m.maria");
        var ticketId = await CreateTicketAsync(creatorClient);
        await creatorClient.PostAsync($"/api/tickets/{ticketId}/escalate", null);

        var response = await headClient.PostAsJsonAsync(
            $"/api/tickets/{ticketId}/escalation/accept",
            new { comment = "РџСЂРёРЅРёРјР°СЋ РІ СЂР°Р±РѕС‚Сѓ" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accepted = await response.Content.ReadFromJsonAsync<TicketEscalationResponse>();
        accepted!.AcceptedByUserId.Should().Be(headId);
        accepted.Comment.Should().Be("РџСЂРёРЅРёРјР°СЋ РІ СЂР°Р±РѕС‚Сѓ");

        var ticket = await headClient.GetFromJsonAsync<TicketResponse>($"/api/tickets/{ticketId}");
        ticket!.StatusName.Should().Be("В работе");
        ticket.AssigneeId.Should().Be(headId);

        var pending = await headClient.GetFromJsonAsync<List<PendingEscalationResponse>>("/api/escalations");
        pending!.Should().NotContain(item => item.TicketId == ticketId);
    }

    [Fact]
    public async Task EscalatingAlreadyEscalatedTicket_IsRejected()
    {
        var (headId, _) = await CreateAgentClientAsync("p.petrov");
        await SetServiceDeskHeadAsync(headId);
        var creatorClient = await CreatePortalClientAsync("m.maria");
        var ticketId = await CreateTicketAsync(creatorClient);
        var firstEscalation = await creatorClient.PostAsync($"/api/tickets/{ticketId}/escalate", null);
        firstEscalation.StatusCode.Should().Be(HttpStatusCode.OK, await firstEscalation.Content.ReadAsStringAsync());

        var second = await creatorClient.PostAsync($"/api/tickets/{ticketId}/escalate", null);

        second.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Accept_WithoutRights_ReturnsForbidden()
    {
        var (headId, _) = await CreateAgentClientAsync("p.petrov");
        await SetServiceDeskHeadAsync(headId);
        var (_, otherAgentClient) = await CreateAgentClientAsync("m.multi");
        var creatorClient = await CreatePortalClientAsync("m.maria");
        var ticketId = await CreateTicketAsync(creatorClient);
        await creatorClient.PostAsync($"/api/tickets/{ticketId}/escalate", null);

        var response = await otherAgentClient.PostAsJsonAsync(
            $"/api/tickets/{ticketId}/escalation/accept",
            new { });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Accept_NonEscalatedTicket_ReturnsConflict()
    {
        var (headId, headClient) = await CreateAgentClientAsync("p.petrov");
        await SetServiceDeskHeadAsync(headId);
        var creatorClient = await CreatePortalClientAsync("m.maria");
        var ticketId = await CreateTicketAsync(creatorClient);

        var response = await headClient.PostAsJsonAsync(
            $"/api/tickets/{ticketId}/escalation/accept",
            new { });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PortalUser_CannotReadEscalationQueue()
    {
        var (headId, _) = await CreateAgentClientAsync("p.petrov");
        await SetServiceDeskHeadAsync(headId);
        var creatorClient = await CreatePortalClientAsync("m.maria");

        var response = await creatorClient.GetAsync("/api/escalations");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EscalationHistoryIsReturnedForTicket()
    {
        var (headId, headClient) = await CreateAgentClientAsync("p.petrov");
        await SetServiceDeskHeadAsync(headId);
        var creatorClient = await CreatePortalClientAsync("m.maria");
        var ticketId = await CreateTicketAsync(creatorClient);
        await creatorClient.PostAsync($"/api/tickets/{ticketId}/escalate", null);
        await headClient.PostAsJsonAsync($"/api/tickets/{ticketId}/escalation/accept", new { });

        var history = await headClient.GetFromJsonAsync<List<TicketEscalationResponse>>($"/api/tickets/{ticketId}/escalations");

        var item = history!.Should().ContainSingle().Subject;
        item.TicketId.Should().Be(ticketId);
        item.AcceptedByUserId.Should().Be(headId);
        item.AcceptedAt.Should().NotBeNull();
    }

    private async Task<HttpClient> CreatePortalClientAsync(string username) =>
        CreateClientFor(username, ["portal_user"]);

    private async Task<(int UserId, HttpClient Client)> CreateAgentClientAsync(string username)
    {
        var client = CreateClientFor(username, ["agent"], out var userId);
        return await Task.FromResult((userId, client));
    }

    private HttpClient CreateClientFor(string username, string[] roles)
    {
        var client = CreateClientFor(username, roles, out _);
        return client;
    }

    private HttpClient CreateClientFor(string username, string[] roles, out int userId)
    {
        var client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();

        var user = db.Users.IgnoreQueryFilters().Include(item => item.RoleMappings).ThenInclude(mapping => mapping.Role).First(item => item.Username == username);
        userId = user.Id;
        var token = jwt.GenerateToken(user.Id, user.Username, user.Email, user.TenantId ?? 1, roles);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task SetServiceDeskHeadAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();
        var tenantId = db.GetCurrentTenantId();

        var setting = await db.ServiceDeskHeadSettings.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == 1);
        if (setting is null)
        {
            db.ServiceDeskHeadSettings.Add(new ServiceDeskHeadSetting { Id = 1, TenantId = tenantId, UserId = userId });
        }
        else
        {
            setting.UserId = userId;
        }

        await db.SaveChangesAsync();
    }

    private static async Task<int> CreateTicketAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = $"Р­СЃРєР°Р»Р°С†РёСЏ {Guid.NewGuid():N}",
            description = "Р—Р°СЏРІРєР° РґР»СЏ РїСЂРѕРІРµСЂРєРё СЌСЃРєР°Р»Р°С†РёРё",
            typeId = 1,
            priority = "РЎСЂРµРґРЅРёР№"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticket = await response.Content.ReadFromJsonAsync<TicketResponse>();
        return ticket!.Id;
    }
}

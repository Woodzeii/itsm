using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace itsm.Tests;

[Collection(IntegrationTestCollection.Name)]
public class TicketPublicNumberApiTests
{
    private readonly CustomWebApplicationFactory _factory;

    static TicketPublicNumberApiTests()
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

    public TicketPublicNumberApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreatedTicketReceivesPublicNumberMatchingMask()
    {
        var client = CreateClient("admin", "admin");

        var created = await CreateTicketAsync(client);

        TicketNumberGenerator.IsValid(created.TicketNumber).Should().BeTrue(created.TicketNumber);
        created.TicketNumber.Should().MatchRegex("^[A-Z]{3}-[0-9]{3}-[0-9]{5}$");
    }

    [Fact]
    public async Task TicketIsReachableByPublicNumberAndById()
    {
        var client = CreateClient("admin", "admin");
        var created = await CreateTicketAsync(client);

        var byNumber = await client.GetFromJsonAsync<TicketResponse>($"/api/tickets/{created.TicketNumber}");
        var byNumberLowerCase = await client.GetFromJsonAsync<TicketResponse>($"/api/tickets/{created.TicketNumber.ToLowerInvariant()}");
        var byId = await client.GetFromJsonAsync<TicketResponse>($"/api/tickets/{created.Id}");

        byNumber!.Id.Should().Be(created.Id);
        byNumberLowerCase!.Id.Should().Be(created.Id);
        byId!.TicketNumber.Should().Be(created.TicketNumber);
    }

    [Fact]
    public async Task UnknownPublicNumberReturnsNotFound()
    {
        var client = CreateClient("admin", "admin");

        var response = await client.GetAsync("/api/tickets/ZZZ-999-99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task QueueSearchFindsTicketByPartialNumber()
    {
        var client = CreateClient("admin", "admin");
        var created = await CreateTicketAsync(client);
        var partial = created.TicketNumber[..3];

        var queue = await client.GetFromJsonAsync<List<TicketResponse>>($"/api/tickets?search={partial}");

        queue!.Should().Contain(ticket => ticket.Id == created.Id);
    }

    [Fact]
    public async Task StatusChangeByPublicNumberWorks()
    {
        var client = CreateClient("admin", "admin");
        var created = await CreateTicketAsync(client);

        var response = await client.PutAsJsonAsync(
            $"/api/tickets/{created.TicketNumber}/status",
            new { statusId = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<TicketResponse>())!.StatusId.Should().Be(2);
    }

    [Fact]
    public async Task PublicNumbersAreUniqueWithinTenant()
    {
        var client = CreateClient("admin", "admin");
        var tickets = new List<TicketResponse>();
        for (var index = 0; index < 5; index++)
            tickets.Add(await CreateTicketAsync(client));

        tickets.Select(ticket => ticket.TicketNumber).Should().OnlyHaveUniqueItems();
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

    private static async Task<TicketResponse> CreateTicketAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = $"Номер заявки {Guid.NewGuid():N}",
            description = "Заявка для проверки публичного номера",
            typeId = 1,
            priority = "Средний"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<TicketResponse>())!;
    }
}

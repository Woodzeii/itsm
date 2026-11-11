using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace itsm.Tests;

[Collection(IntegrationTestCollection.Name)]
public class AssetsApiTests
{
    private readonly CustomWebApplicationFactory _factory;

    static AssetsApiTests()
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

    public AssetsApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateAsset_WithDuplicateInventoryNumber_ReturnsConflict()
    {
        var client = await CreateAgentClientAsync();
        var assetClassId = await CreateAssetClassAsync();

        var first = await client.PostAsJsonAsync("/api/assets", new
        {
            name = "Ноутбук Dell",
            inventoryNumber = "inv-001",
            serialNumber = "sn-001",
            assetClassId
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicateInventory = await client.PostAsJsonAsync("/api/assets", new
        {
            name = "Ноутбук HP",
            inventoryNumber = "INV-001",
            serialNumber = "sn-002",
            assetClassId
        });

        duplicateInventory.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateAsset_WithDuplicateSerialNumber_ReturnsConflict()
    {
        var client = await CreateAgentClientAsync();
        var assetClassId = await CreateAssetClassAsync();

        var first = await client.PostAsJsonAsync("/api/assets", new
        {
            name = "Монитор",
            inventoryNumber = $"INV-{Guid.NewGuid():N}",
            serialNumber = "SN-DUP",
            assetClassId
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicateSerial = await client.PostAsJsonAsync("/api/assets", new
        {
            name = "Монитор 2",
            inventoryNumber = $"INV-{Guid.NewGuid():N}",
            serialNumber = "sn-dup",
            assetClassId
        });

        duplicateSerial.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateAsset_WithoutAnyIdentifier_ReturnsBadRequest()
    {
        var client = await CreateAgentClientAsync();
        var assetClassId = await CreateAssetClassAsync();

        var response = await client.PostAsJsonAsync("/api/assets", new
        {
            name = "Без номера",
            assetClassId
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAsset_WithUnknownAssetClass_ReturnsBadRequest()
    {
        var client = await CreateAgentClientAsync();

        var response = await client.PostAsJsonAsync("/api/assets", new
        {
            name = "Актив",
            inventoryNumber = $"INV-{Guid.NewGuid():N}",
            assetClassId = 999_999
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateAsset_ToExistingInventoryNumber_ReturnsConflict()
    {
        var client = await CreateAgentClientAsync();
        var assetClassId = await CreateAssetClassAsync();

        var first = await CreateAssetAsync(client, assetClassId, "INV-A1");
        var second = await CreateAssetAsync(client, assetClassId, "INV-A2");

        var response = await client.PutAsJsonAsync($"/api/assets/{second.Id}", new
        {
            inventoryNumber = "INV-A1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public async Task IssuedAsset_ChangesLifecycleStageAndWritesHistory()
    {
        var client = await CreateAgentClientAsync();
        var assetClassId = await CreateAssetClassAsync();
        var warehouseId = await CreateWarehouseAsync();
        var employeeId = await CreateEmployeeAsync();

        var created = await CreateAssetAsync(client, assetClassId, $"INV-{Guid.NewGuid():N}", warehouseId);

        var movement = await client.PostAsJsonAsync($"/api/assets/{created.Id}/movements", new
        {
            movementType = AssetMovementTypes.Issue,
            toUserId = employeeId
        });
        movement.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await client.GetFromJsonAsync<AssetDetailDto>($"/api/assets/{created.Id}");
        detail.Should().NotBeNull();
        detail!.Asset.LifecycleStage.Should().Be(AssetLifecycleStages.InUse);
        detail.Asset.AssignedUserId.Should().Be(employeeId);
        detail.History.Should().Contain(item => item.ActionType == AssetHistoryActionTypes.StageChanged);
        detail.History.Should().Contain(item => item.ActionType == AssetHistoryActionTypes.Movement);
    }

    [Fact]
    public async Task IssueAsset_FromWrongStage_ReturnsConflict()
    {
        var client = await CreateAgentClientAsync();
        var assetClassId = await CreateAssetClassAsync();
        var employeeId = await CreateEmployeeAsync();

        var created = await CreateAssetAsync(client, assetClassId, $"INV-{Guid.NewGuid():N}");

        var movement = await client.PostAsJsonAsync($"/api/assets/{created.Id}/movements", new
        {
            movementType = AssetMovementTypes.Issue,
            toUserId = employeeId
        });

        movement.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteAsset_NotInPurchasedStage_ReturnsConflict()
    {
        var client = await CreateAdminClientAsync();
        var assetClassId = await CreateAssetClassAsync();
        var warehouseId = await CreateWarehouseAsync();

        var created = await CreateAssetAsync(client, assetClassId, $"INV-{Guid.NewGuid():N}", warehouseId);

        var response = await client.DeleteAsync($"/api/assets/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PortalUser_CannotAccessAssets()
    {
        var client = await CreatePortalClientAsync();

        var response = await client.GetAsync("/api/assets");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> CreatePortalClientAsync()
    {
        var client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();

        var portalUser = await db.Users.IgnoreQueryFilters().FirstAsync(user => user.Username == "s.sidorov");
        var token = jwt.GenerateToken(portalUser.Id, portalUser.Username, portalUser.Email, portalUser.TenantId ?? 1, ["portal_user"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();

        var admin = await db.Users.IgnoreQueryFilters().FirstAsync(user => user.Username == "admin");
        var token = jwt.GenerateToken(admin.Id, admin.Username, admin.Email, admin.TenantId ?? 1, ["admin"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> CreateAgentClientAsync()
    {
        var client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();

        var agent = await db.Users.IgnoreQueryFilters().FirstAsync(user => user.Username == "p.petrov");
        var token = jwt.GenerateToken(agent.Id, agent.Username, agent.Email, agent.TenantId ?? 1, ["agent"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<int> CreateAssetClassAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();

        var assetClass = new AssetClass
        {
            Code = $"class_{Guid.NewGuid():N}"[..20],
            Name = "Тестовый класс",
            IsActive = true
        };
        db.AssetClasses.Add(assetClass);
        await db.SaveChangesAsync();
        return assetClass.Id;
    }

    private async Task<int> CreateWarehouseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();
        return await db.DictionaryValues
            .Where(value => value.Dictionary.Code == "warehouses" && !value.IsArchived)
            .Select(value => value.Id)
            .FirstAsync();
    }

    private async Task<int> CreateEmployeeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();
        return await db.Users.IgnoreQueryFilters().Where(user => user.Username == "a.anna").Select(user => user.Id).FirstAsync();
    }

    private static async Task<AssetDto> CreateAssetAsync(
        HttpClient client,
        int assetClassId,
        string inventoryNumber,
        int? warehouseId = null)
    {
        var response = await client.PostAsJsonAsync("/api/assets", new
        {
            name = $"Актив {inventoryNumber}",
            inventoryNumber,
            assetClassId,
            warehouseId
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AssetDto>())!;
    }
}

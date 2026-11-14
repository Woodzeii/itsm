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
            assetClassCode = assetClassId
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicateInventory = await client.PostAsJsonAsync("/api/assets", new
        {
            name = "Ноутбук HP",
            inventoryNumber = "INV-001",
            serialNumber = "sn-002",
            assetClassCode = assetClassId
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
            assetClassCode = assetClassId
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicateSerial = await client.PostAsJsonAsync("/api/assets", new
        {
            name = "Монитор 2",
            inventoryNumber = $"INV-{Guid.NewGuid():N}",
            serialNumber = "sn-dup",
            assetClassCode = assetClassId
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
            assetClassCode = assetClassId
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAsset_WithWarehouseButWithoutInventoryNumber_ReturnsBadRequest()
    {
        var client = await CreateAgentClientAsync();
        var assetClassId = await CreateAssetClassAsync();
        var warehouseId = await CreateWarehouseAsync();

        var response = await client.PostAsJsonAsync("/api/assets", new
        {
            name = "Актив только с серийным номером",
            serialNumber = $"SN-{Guid.NewGuid():N}",
            assetClassCode = assetClassId,
            warehouseCode = warehouseId
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAsset_CannotChooseLifecycleStageOrAssignedUser()
    {
        var client = await CreateAgentClientAsync();
        var assetClassId = await CreateAssetClassAsync();
        var employeeId = await CreateEmployeeAsync();

        var stageResponse = await client.PostAsJsonAsync("/api/assets", new
        {
            name = "Актив с заданным этапом",
            inventoryNumber = $"INV-{Guid.NewGuid():N}",
            assetClassCode = assetClassId,
            lifecycleStage = AssetLifecycleStages.InUse
        });
        var assignedResponse = await client.PostAsJsonAsync("/api/assets", new
        {
            name = "Актив с назначенным сотрудником",
            inventoryNumber = $"INV-{Guid.NewGuid():N}",
            assetClassCode = assetClassId,
            assignedUserId = employeeId
        });

        stageResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        assignedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAsset_WithoutWarehouseStartsAsPurchased()
    {
        var client = await CreateAgentClientAsync();
        var assetClassId = await CreateAssetClassAsync();

        var created = await CreateAssetAsync(client, assetClassId, $"INV-{Guid.NewGuid():N}");

        created.LifecycleStage.Should().Be(AssetLifecycleStages.Purchased);
    }

    [Fact]
    public async Task AssetAndDictionaryResponses_UseCodesAndPreventDeletingReferencedValues()
    {
        var client = await CreateAgentClientAsync();
        var adminClient = await CreateAdminClientAsync();
        var assetClassCode = await CreateAssetClassAsync();
        var warehouseCode = await CreateWarehouseAsync();
        var locationCode = await CreateDictionaryValueAsync("locations");
        var departmentCode = await CreateDictionaryValueAsync("departments");

        var asset = await CreateAssetAsync(
            client,
            assetClassCode,
            $"INV-{Guid.NewGuid():N}",
            warehouseCode,
            locationCode,
            departmentCode);
        asset.AssetClassCode.Should().Be(assetClassCode);
        asset.AssetClassName.Should().Be("Тестовый класс");
        asset.WarehouseCode.Should().Be(warehouseCode);
        asset.WarehouseName.Should().Be("Тестовый склад");
        asset.LocationCode.Should().Be(locationCode);
        asset.LocationName.Should().Be("Тестовая локация");
        asset.DepartmentCode.Should().Be(departmentCode);
        asset.DepartmentName.Should().Be("Тестовое подразделение");

        var valuesResponse = await client.GetAsync("/api/dictionaries/warehouses/values");
        valuesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var values = (await valuesResponse.Content.ReadFromJsonAsync<List<DictionaryValueDto>>())!;
        values.Should().Contain(value =>
            value.DictionaryCode == "warehouses" && value.Code == warehouseCode);

        foreach (var (dictionaryCode, valueCode, field) in new[]
                 {
                     ("warehouses", warehouseCode, "WarehouseCode"),
                     ("locations", locationCode, "LocationCode"),
                     ("departments", departmentCode, "DepartmentCode")
                 })
        {
            var archiveResponse = await adminClient.PostAsync(
                $"/api/dictionaries/{dictionaryCode}/values/{valueCode}/archive", content: null);
            archiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var archivedAsset = await client.GetFromJsonAsync<AssetDetailDto>($"/api/assets/{asset.Id}");
            var reference = dictionaryCode switch
            {
                "warehouses" => archivedAsset!.Asset.WarehouseCode,
                "locations" => archivedAsset!.Asset.LocationCode,
                _ => archivedAsset!.Asset.DepartmentCode
            };
            reference.Should().Be(valueCode);

            if (dictionaryCode == "warehouses")
            {
                var invalidAssignment = await client.PostAsJsonAsync("/api/assets", new
                {
                    name = "Актив с архивным складом",
                    inventoryNumber = $"INV-{Guid.NewGuid():N}",
                    assetClassCode,
                    warehouseCode = valueCode
                });
                invalidAssignment.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                var problem = await invalidAssignment.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
                problem!.Errors.Should().ContainKey(field);
            }

            var deleteResponse = await adminClient.DeleteAsync(
                $"/api/dictionaries/{dictionaryCode}/values/{valueCode}");
            deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

            var restoreResponse = await adminClient.PostAsync(
                $"/api/dictionaries/{dictionaryCode}/values/{valueCode}/restore", content: null);
            restoreResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task CreateAsset_RejectsWarehouseCodeFromAnotherDictionaryWithFieldError()
    {
        var client = await CreateAgentClientAsync();
        var assetClassCode = await CreateAssetClassAsync();
        var locationCode = await CreateDictionaryValueAsync("locations");

        var response = await client.PostAsJsonAsync("/api/assets", new
        {
            name = "Актив",
            inventoryNumber = $"INV-{Guid.NewGuid():N}",
            assetClassCode,
            warehouseCode = locationCode
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        problem!.Errors.Should().ContainKey("WarehouseCode");
    }

    [Fact]
    public async Task DictionaryLifecycleAndValueRoutesUseCodes()
    {
        var client = await CreateAdminClientAsync();
        var dictionaryCode = $"test_{Guid.NewGuid():N}"[..20];
        var createDictionary = await client.PostAsJsonAsync("/api/dictionaries", new
        {
            code = dictionaryCode,
            name = "Тестовый справочник"
        });
        createDictionary.StatusCode.Should().Be(HttpStatusCode.Created);
        var dictionary = (await createDictionary.Content.ReadFromJsonAsync<DictionaryDto>())!;
        dictionary.Code.Should().Be(dictionaryCode);

        var createValue = await client.PostAsJsonAsync(
            $"/api/dictionaries/{dictionaryCode}/values",
            new { code = "sample", name = "Пример", sortOrder = 0 });
        createValue.StatusCode.Should().Be(HttpStatusCode.OK);
        var value = (await createValue.Content.ReadFromJsonAsync<DictionaryValueDto>())!;
        value.DictionaryCode.Should().Be(dictionaryCode);
        value.Code.Should().Be("sample");

        var archiveDictionary = await client.PostAsync(
            $"/api/dictionaries/{dictionaryCode}/archive", content: null);
        archiveDictionary.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/dictionaries/{dictionaryCode}/values")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        var restoreDictionary = await client.PostAsync(
            $"/api/dictionaries/{dictionaryCode}/restore", content: null);
        restoreDictionary.StatusCode.Should().Be(HttpStatusCode.OK);

        var archiveValue = await client.PostAsync(
            $"/api/dictionaries/{dictionaryCode}/values/sample/archive", content: null);
        archiveValue.StatusCode.Should().Be(HttpStatusCode.OK);
        var restoreValue = await client.PostAsync(
            $"/api/dictionaries/{dictionaryCode}/values/sample/restore", content: null);
        restoreValue.StatusCode.Should().Be(HttpStatusCode.OK);
        var archiveForDelete = await client.PostAsync(
            $"/api/dictionaries/{dictionaryCode}/values/sample/archive", content: null);
        archiveForDelete.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteValue = await client.DeleteAsync($"/api/dictionaries/{dictionaryCode}/values/sample");
        deleteValue.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var deleteDictionary = await client.DeleteAsync($"/api/dictionaries/{dictionaryCode}");
        deleteDictionary.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AssetAttributeValues_ArePersistedOnCreateAndUpdate()
    {
        var client = await CreateAgentClientAsync();
        var assetClassId = await CreateAssetClassAsync();
        var createResponse = await client.PostAsJsonAsync("/api/assets", new
        {
            name = "Ноутбук",
            inventoryNumber = $"INV-{Guid.NewGuid():N}",
            assetClassCode = assetClassId,
            attributes = new Dictionary<string, string> { ["ram"] = "16" }
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await createResponse.Content.ReadFromJsonAsync<AssetDto>())!;
        created.Attributes.Should().ContainKey("ram").WhoseValue.Should().Be("16");

        var updateResponse = await client.PutAsJsonAsync($"/api/assets/{created.Id}", new
        {
            attributes = new Dictionary<string, string>
            {
                ["ram"] = "32",
                ["cpu"] = "Core i7"
            }
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = (await client.GetFromJsonAsync<AssetDetailDto>($"/api/assets/{created.Id}"))!;
        detail.Asset.Attributes.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["ram"] = "32",
            ["cpu"] = "Core i7"
        });
    }

    [Fact]
    public async Task CreateAsset_WithUnknownAssetClass_ReturnsBadRequest()
    {
        var client = await CreateAgentClientAsync();

        var response = await client.PostAsJsonAsync("/api/assets", new
        {
            name = "Актив",
            inventoryNumber = $"INV-{Guid.NewGuid():N}",
            assetClassCode = "missing_class"
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
    public async Task UpdateAsset_CannotChangeWarehouseWithoutMovement()
    {
        var client = await CreateAgentClientAsync();
        var assetClassId = await CreateAssetClassAsync();
        var currentWarehouseId = await CreateWarehouseAsync();
        var newWarehouseId = await CreateWarehouseAsync();
        var created = await CreateAssetAsync(client, assetClassId, $"INV-{Guid.NewGuid():N}", currentWarehouseId);

        var response = await client.PutAsJsonAsync($"/api/assets/{created.Id}", new
        {
            warehouseCode = newWarehouseId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RelocateAsset_BetweenWarehousesKeepsItInStock()
    {
        var client = await CreateAgentClientAsync();
        var assetClassId = await CreateAssetClassAsync();
        var currentWarehouseId = await CreateWarehouseAsync();
        var targetWarehouseId = await CreateWarehouseAsync();
        var created = await CreateAssetAsync(client, assetClassId, $"INV-{Guid.NewGuid():N}", currentWarehouseId);

        var movement = await client.PostAsJsonAsync($"/api/assets/{created.Id}/movements", new
        {
            movementType = AssetMovementTypes.Relocate,
            toWarehouseCode = targetWarehouseId
        });
        var detail = await client.GetFromJsonAsync<AssetDetailDto>($"/api/assets/{created.Id}");

        movement.StatusCode.Should().Be(HttpStatusCode.OK);
        detail!.Asset.LifecycleStage.Should().Be(AssetLifecycleStages.InStock);
        detail.Asset.WarehouseCode.Should().Be(targetWarehouseId);
    }

    [Fact]
    public async Task PurchasedAsset_CanBeReceivedIntoWarehouse()
    {
        var client = await CreateAgentClientAsync();
        var assetClassId = await CreateAssetClassAsync();
        var warehouseCode = await CreateWarehouseAsync();
        var created = await CreateAssetAsync(client, assetClassId, $"INV-{Guid.NewGuid():N}");

        var movement = await client.PostAsJsonAsync($"/api/assets/{created.Id}/movements", new
        {
            movementType = AssetMovementTypes.Stocking,
            toWarehouseCode = warehouseCode
        });
        var detail = await client.GetFromJsonAsync<AssetDetailDto>($"/api/assets/{created.Id}");

        movement.StatusCode.Should().Be(HttpStatusCode.OK);
        detail!.Asset.LifecycleStage.Should().Be(AssetLifecycleStages.InStock);
        detail.Asset.WarehouseCode.Should().Be(warehouseCode);
        detail.History.Should().Contain(item => item.ActionType == AssetHistoryActionTypes.StageChanged);
        detail.Movements.Should().Contain(item => item.MovementType == AssetMovementTypes.Stocking);
    }

    [Fact]
    public async Task PurchasedAssetWithoutInventoryNumber_CannotBeReceivedIntoWarehouse()
    {
        var client = await CreateAgentClientAsync();
        var assetClassCode = await CreateAssetClassAsync();
        var warehouseCode = await CreateWarehouseAsync();
        var createResponse = await client.PostAsJsonAsync("/api/assets", new
        {
            name = "Актив только с серийным номером",
            serialNumber = $"SN-{Guid.NewGuid():N}",
            assetClassCode
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await createResponse.Content.ReadFromJsonAsync<AssetDto>())!;

        var movement = await client.PostAsJsonAsync($"/api/assets/{created.Id}/movements", new
        {
            movementType = AssetMovementTypes.Stocking,
            toWarehouseCode = warehouseCode
        });

        movement.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TransferMovement_IsNotAllowedByLifecycle()
    {
        var client = await CreateAgentClientAsync();
        var assetClassId = await CreateAssetClassAsync();
        var warehouseId = await CreateWarehouseAsync();
        var employeeId = await CreateEmployeeAsync();
        var created = await CreateAssetAsync(client, assetClassId, $"INV-{Guid.NewGuid():N}", warehouseId);

        var movement = await client.PostAsJsonAsync($"/api/assets/{created.Id}/movements", new
        {
            movementType = "transfer",
            toUserId = employeeId
        });

        movement.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
    public async Task IssuedAsset_CanReturnToWarehouseThroughMovement()
    {
        var client = await CreateAgentClientAsync();
        var assetClassId = await CreateAssetClassAsync();
        var initialWarehouseId = await CreateWarehouseAsync();
        var returnWarehouseId = await CreateWarehouseAsync();
        var employeeId = await CreateEmployeeAsync();
        var created = await CreateAssetAsync(client, assetClassId, $"INV-{Guid.NewGuid():N}", initialWarehouseId);

        var issue = await client.PostAsJsonAsync($"/api/assets/{created.Id}/movements", new
        {
            movementType = AssetMovementTypes.Issue,
            toUserId = employeeId
        });
        var returned = await client.PostAsJsonAsync($"/api/assets/{created.Id}/movements", new
        {
            movementType = AssetMovementTypes.Return,
            toWarehouseCode = returnWarehouseId
        });
        var detail = await client.GetFromJsonAsync<AssetDetailDto>($"/api/assets/{created.Id}");

        issue.StatusCode.Should().Be(HttpStatusCode.OK);
        returned.StatusCode.Should().Be(HttpStatusCode.OK);
        detail!.Asset.LifecycleStage.Should().Be(AssetLifecycleStages.InStock);
        detail.Asset.AssignedUserId.Should().BeNull();
        detail.Asset.WarehouseCode.Should().Be(returnWarehouseId);
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

    private async Task<string> CreateAssetClassAsync()
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
        return assetClass.Code;
    }

    private async Task<string> CreateWarehouseAsync()
        => await CreateDictionaryValueAsync("warehouses");

    private async Task<string> CreateDictionaryValueAsync(string dictionaryCode)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();
        var dictionary = await db.Dictionaries.SingleAsync(item => item.Code == dictionaryCode);
        var value = new DictionaryValue
        {
            DictionaryId = dictionary.Id,
            Code = $"test-{Guid.NewGuid():N}",
            Name = dictionaryCode switch
            {
                "warehouses" => "Тестовый склад",
                "locations" => "Тестовая локация",
                _ => "Тестовое подразделение"
            },
            SortOrder = 0
        };
        db.DictionaryValues.Add(value);
        await db.SaveChangesAsync();
        return value.Code;
    }

    private async Task<int> CreateEmployeeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ItsmDbContext>();
        return await db.Users.IgnoreQueryFilters().Where(user => user.Username == "a.anna").Select(user => user.Id).FirstAsync();
    }

    private static async Task<AssetDto> CreateAssetAsync(
        HttpClient client,
        string assetClassCode,
        string inventoryNumber,
        string? warehouseCode = null,
        string? locationCode = null,
        string? departmentCode = null)
    {
        var response = await client.PostAsJsonAsync("/api/assets", new
        {
            name = $"Актив {inventoryNumber}",
            inventoryNumber,
            assetClassCode,
            warehouseCode,
            locationCode,
            departmentCode
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AssetDto>())!;
    }
}

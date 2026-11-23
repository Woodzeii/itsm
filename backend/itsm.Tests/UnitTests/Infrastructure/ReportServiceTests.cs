using FluentAssertions;
using itsm.Domain.Constants;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using itsm.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;

namespace itsm.Tests.UnitTests.Infrastructure;

public class ReportServiceTests
{
    [Fact]
    public async Task StatusAndLoadReports_ShowCurrentQueueIncludingZeroStatusesAndUnassignedTickets()
    {
        var options = new DbContextOptionsBuilder<ItsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetCurrentTenantId(1);
        await using var db = new ItsmDbContext(options, tenantContext);
        await db.Database.EnsureCreatedAsync();

        var creator = new User
        {
            Username = "requester",
            Email = "requester@example.test",
            FullName = "Автор",
            TenantId = 1,
        };
        var engineer = new User
        {
            Username = "engineer",
            Email = "engineer@example.test",
            FullName = "Инженер",
            TenantId = 1,
        };
        db.Users.AddRange(creator, engineer);
        await db.SaveChangesAsync();

        var inPeriod = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        db.Tickets.AddRange(
            CreateTicket("T-001", creator.Id, engineer.Id, 3, inPeriod),
            CreateTicket("T-002", creator.Id, engineer.Id, 3, inPeriod.AddDays(1)),
            CreateTicket("T-003", creator.Id, engineer.Id, 5, inPeriod.AddDays(2)),
            CreateTicket("T-004", creator.Id, null, 3, inPeriod.AddDays(3)),
            CreateTicket("T-005", creator.Id, engineer.Id, 3, inPeriod.AddMonths(-1)));
        await db.SaveChangesAsync();

        var service = new ReportService(db);
        var summaries = await service.GetSummariesAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var engineerLoad = await service.GetDetailsAsync(
            "engineer-load",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31),
            0,
            30);

        summaries.Single(report => report.Id == "tickets-status").Count.Should().Be(4);
        summaries.Single(report => report.Id == "engineer-load").Count.Should().Be(4);
        engineerLoad.Should().NotBeNull();
        engineerLoad!.Total.Should().Be(2);
        engineerLoad.Items[0].Values["assignee"].Should().Be("Инженер");
        engineerLoad.Items[0].Values["count"].Should().Be("3");
        engineerLoad.Items[1].Values["assignee"].Should().Be("Не назначено");
        engineerLoad.Items[1].Values["count"].Should().Be("1");

        var statuses = await service.GetDetailsAsync("tickets-status", null, null, 0, 30);
        statuses.Should().NotBeNull();
        statuses!.Items.Should().Contain(item =>
            item.Values["status"] == "Ожидает выполнения" && item.Values["count"] == "0");
    }

    [Fact]
    public async Task TicketTypesReport_GroupsByTypeAndCriticalityForCreationPeriod()
    {
        var options = new DbContextOptionsBuilder<ItsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetCurrentTenantId(1);
        await using var db = new ItsmDbContext(options, tenantContext);
        await db.Database.EnsureCreatedAsync();

        var creator = new User
        {
            Username = "type-requester",
            Email = "type-requester@example.test",
            FullName = "Автор",
            TenantId = 1,
        };
        db.Users.Add(creator);
        await db.SaveChangesAsync();
        var createdAt = new DateTimeOffset(2026, 2, 10, 10, 0, 0, TimeSpan.Zero);
        db.Tickets.AddRange(
            CreateTicket("T-101", creator.Id, null, 1, createdAt, 1),
            CreateTicket("T-102", creator.Id, null, 1, createdAt.AddDays(1), 1),
            CreateTicket("T-103", creator.Id, null, 1, createdAt.AddMonths(-1), 2));
        await db.SaveChangesAsync();

        var detail = await new ReportService(db).GetDetailsAsync(
            "tickets-type",
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 28),
            0,
            30);

        detail.Should().NotBeNull();
        detail!.Total.Should().Be(1);
        detail.Items.Single().Values["count"].Should().Be("2");
        detail.Items.Single().Values["criticality"].Should().NotBe("Не указана");
    }

    [Fact]
    public async Task SlaReport_UsesEnabledTypeCriticalityTargetsAndCountsOverdueTickets()
    {
        var (db, creator, _) = await CreateDatabase();
        var createdAt = DateTimeOffset.UtcNow.AddHours(-4);
        db.SlaValues.Add(new SlaValue
        {
            TicketTypeId = 1,
            CriticalityLevelId = 1,
            ReactionTimeMinutes = 30,
            ResolutionTimeMinutes = 60,
            IsDisabled = false,
        });
        db.Tickets.Add(CreateTicket("SLA-1", creator.Id, null, 1, createdAt, 1));
        await db.SaveChangesAsync();

        var detail = await new ReportService(db).GetDetailsAsync("sla-compliance", null, null, 0, 30);

        detail.Should().NotBeNull();
        detail!.Total.Should().Be(1);
        detail.Items.Single().Values["reactionCount"].Should().Be("1");
        detail.Items.Single().Values["reactionPercent"].Should().Be("100%");
        detail.Items.Single().Values["resolutionCount"].Should().Be("1");
        detail.Items.Single().Values["resolutionPercent"].Should().Be("100%");
    }

    [Fact]
    public async Task EngineerPerformance_UsesClosureAuditAndPublicFirstResponseTime()
    {
        var (db, creator, engineer) = await CreateDatabase();
        var createdAt = new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);
        var closedAt = createdAt.AddHours(2);
        var ticket = CreateTicket("PERF-1", creator.Id, engineer.Id, 5, createdAt);
        ticket.ActualResolutionTime = closedAt;
        ticket.SlaAccumulatedPauseMinutes = 15;
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        db.TicketAuditLogs.Add(new TicketAuditLog
        {
            TicketId = ticket.Id,
            UserId = engineer.Id,
            FieldName = "statusId",
            OldValue = "4",
            NewValue = "5",
            ChangedAt = closedAt,
        });
        db.TicketMessages.Add(new TicketMessage
        {
            TicketId = ticket.Id,
            AuthorId = engineer.Id,
            MessageBody = "Принял в работу",
            IsInternal = false,
            CreatedAt = createdAt.AddMinutes(30),
        });
        await db.SaveChangesAsync();

        var detail = await new ReportService(db).GetDetailsAsync(
            "engineer-performance",
            new DateOnly(2026, 3, 15),
            new DateOnly(2026, 3, 15),
            0,
            30);

        detail.Should().NotBeNull();
        detail!.Items.Should().ContainSingle();
        detail.Items.Single().Values["closed"].Should().Be("1");
        detail.Items.Single().Values["reaction"].Should().Be("00:30");
        detail.Items.Single().Values["resolution"].Should().Be("01:45");
    }

    [Fact]
    public async Task ReturnReport_GroupsReviewToPendingAuditEventsByAssignedEngineer()
    {
        var (db, creator, engineer) = await CreateDatabase();
        var ticket = CreateTicket("RETURN-1", creator.Id, engineer.Id, 2, DateTimeOffset.UtcNow);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        var returnedAt = new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero);
        db.TicketAuditLogs.AddRange(
            new TicketAuditLog
            {
                TicketId = ticket.Id,
                UserId = engineer.Id,
                FieldName = "assigneeId",
                NewValue = engineer.Id.ToString(),
                ChangedAt = returnedAt.AddMinutes(-20),
            },
            new TicketAuditLog
            {
                TicketId = ticket.Id,
                UserId = creator.Id,
                FieldName = "statusId",
                OldValue = "4",
                NewValue = "2",
                ChangedAt = returnedAt,
            });
        await db.SaveChangesAsync();

        var detail = await new ReportService(db).GetDetailsAsync(
            "returns",
            new DateOnly(2026, 4, 10),
            new DateOnly(2026, 4, 10),
            0,
            30);

        detail.Should().NotBeNull();
        detail!.Items.Should().ContainSingle();
        detail.Items.Single().Values["assignee"].Should().Be(engineer.FullName);
        detail.Items.Single().Values["count"].Should().Be("1");
    }

    [Fact]
    public async Task RepairReport_UsesRepairCloseAuditOutcomeAndAssetClass()
    {
        var (db, creator, _) = await CreateDatabase();
        var assetClass = new AssetClass { Code = "laptop", Name = "Ноутбуки" };
        db.AssetClasses.Add(assetClass);
        await db.SaveChangesAsync();
        var asset = new Asset
        {
            TenantId = 1,
            InventoryNumber = "INV-1",
            Name = "Рабочая станция",
            AssetClassId = assetClass.Id,
            LifecycleStage = AssetLifecycleStages.InUse,
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        var closedAt = new DateTimeOffset(2026, 5, 5, 9, 0, 0, TimeSpan.Zero);
        var ticket = CreateTicket("REPAIR-1", creator.Id, null, 5, closedAt);
        ticket.TypeId = 5;
        ticket.AssetId = asset.Id;
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        db.AssetHistoryLogs.Add(new AssetHistoryLog
        {
            AssetId = asset.Id,
            TicketId = ticket.Id,
            UserId = creator.Id,
            ActionType = AssetHistoryActionTypes.TicketClosed,
            Details = """{"repairOutcome":"ReturnToExploitation"}""",
            ChangedAt = closedAt,
        });
        await db.SaveChangesAsync();

        var detail = await new ReportService(db).GetDetailsAsync(
            "repairs",
            new DateOnly(2026, 5, 5),
            new DateOnly(2026, 5, 5),
            0,
            30);

        detail.Should().NotBeNull();
        detail!.Items.Should().ContainSingle();
        detail.Items.Single().Values["assetClass"].Should().Be("Ноутбуки");
        detail.Items.Single().Values["asset"].Should().Be("Рабочая станция");
        detail.Items.Single().Values["outcome"].Should().Be("В эксплуатацию");
    }

    [Fact]
    public async Task AssetStageReport_ReturnsClassByStageMatrixWithZeroCounts()
    {
        var (db, _, _) = await CreateDatabase();
        var assetClass = new AssetClass { Code = "monitor", Name = "Мониторы" };
        db.AssetClasses.Add(assetClass);
        await db.SaveChangesAsync();
        db.Assets.Add(new Asset
        {
            TenantId = 1,
            InventoryNumber = "INV-2",
            Name = "Монитор",
            AssetClassId = assetClass.Id,
            LifecycleStage = AssetLifecycleStages.InStock,
        });
        await db.SaveChangesAsync();

        var detail = await new ReportService(db).GetDetailsAsync(
            "assets-stage",
            new DateOnly(1900, 1, 1),
            new DateOnly(1900, 1, 2),
            0,
            30);

        detail.Should().NotBeNull();
        detail!.Columns.Should().HaveCount(6);
        detail.Items.Should().Contain(item =>
            item.Values["assetClass"] == "Мониторы"
            && item.Values[AssetLifecycleStages.InStock] == "1"
            && item.Values[AssetLifecycleStages.InUse] == "0");
    }

    [Fact]
    public async Task MovementReport_ResolvesFromAndToPlacesForTheSelectedPeriod()
    {
        var (db, creator, engineer) = await CreateDatabase();
        var warehouse = new itsm.Domain.Entities.Dictionary
        {
            TenantId = 1,
            Code = "warehouses",
            Name = "Склады теста",
            IsSystem = true,
        };
        db.Dictionaries.Add(warehouse);
        await db.SaveChangesAsync();
        var warehouseValue = new DictionaryValue
        {
            DictionaryId = warehouse.Id,
            Code = "main",
            Name = "Основной склад",
        };
        db.DictionaryValues.Add(warehouseValue);
        await db.SaveChangesAsync();
        warehouseValue.Id.Should().BeGreaterThan(0);
        var storedWarehouse = await db.DictionaryValues.AsNoTracking()
            .Where(value => value.Id == warehouseValue.Id)
            .Select(value => new { value.Name, value.Dictionary.TenantId, value.Dictionary.Code })
            .SingleAsync();
        storedWarehouse.Name.Should().Be("Основной склад");
        storedWarehouse.TenantId.Should().Be(1);
        storedWarehouse.Code.Should().Be("warehouses");
        var asset = new Asset
        {
            TenantId = 1,
            InventoryNumber = "INV-3",
            Name = "Ноутбук",
            LifecycleStage = AssetLifecycleStages.InStock,
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        var movedAt = new DateTimeOffset(2026, 6, 6, 8, 0, 0, TimeSpan.Zero);
        db.AssetMovements.Add(new AssetMovement
        {
            AssetId = asset.Id,
            MovementType = AssetMovementTypes.Issue,
            FromWarehouseId = warehouseValue.Id,
            ToUserId = engineer.Id,
            PerformedById = creator.Id,
            PerformedAt = movedAt,
        });
        await db.SaveChangesAsync();
        (await db.AssetMovements.SingleAsync()).FromWarehouseId.Should().Be(warehouseValue.Id);
        (await db.DictionaryValues.AsNoTracking().Where(value =>
            value.Id == warehouseValue.Id
            && !value.IsArchived
            && !value.Dictionary.IsArchived
            && value.Dictionary.TenantId == db.GetCurrentTenantId()).CountAsync()).Should().Be(1);

        var detail = await new ReportService(db).GetDetailsAsync(
            "asset-movements",
            new DateOnly(2026, 6, 6),
            new DateOnly(2026, 6, 6),
            0,
            30);

        detail.Should().NotBeNull();
        detail!.Items.Should().ContainSingle();
        detail.Items.Single().Values["asset"].Should().Contain("Ноутбук");
        detail.Items.Single().Values["from"].Should().Contain("Основной склад");
        detail.Items.Single().Values["to"].Should().Be(engineer.FullName);
        detail.Items.Single().Values["actor"].Should().Be(creator.FullName);
    }

    private static async Task<(ItsmDbContext Db, User Creator, User Engineer)> CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<ItsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetCurrentTenantId(1);
        var db = new ItsmDbContext(options, tenantContext);
        await db.Database.EnsureCreatedAsync();
        var creator = new User
        {
            Username = $"creator-{Guid.NewGuid():N}",
            Email = $"creator-{Guid.NewGuid():N}@example.test",
            FullName = "Автор",
            TenantId = 1,
        };
        var engineer = new User
        {
            Username = $"engineer-{Guid.NewGuid():N}",
            Email = $"engineer-{Guid.NewGuid():N}@example.test",
            FullName = "Инженер",
            TenantId = 1,
        };
        db.Users.AddRange(creator, engineer);
        await db.SaveChangesAsync();
        return (db, creator, engineer);
    }

    private static Ticket CreateTicket(
        string number,
        int creatorId,
        int? assigneeId,
        int statusId,
        DateTimeOffset createdAt,
        int? criticalityLevelId = null) => new()
    {
        TenantId = 1,
        TicketNumber = number,
        Title = number,
        TypeId = 1,
        StatusId = statusId,
        CreatorId = creatorId,
        AssigneeId = assigneeId,
        CreatedAt = createdAt,
        CriticalityLevelId = criticalityLevelId,
    };
}

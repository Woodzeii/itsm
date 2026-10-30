using System.Text;
using System.Text.Json;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using itsm.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace itsm.Tests.IntegrationTests;

[Collection(TicketServiceTestCollection.Name)]
public sealed class TicketServiceIntegrationTests(TicketServiceFixture fixture)
{
    [Fact]
    public async Task CreateTicketPersistsInitialStatusAndAudit()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);

        var ticket = await service.CreateAsync(
            NewTicketRequest(),
            creator.Id,
            isStaff: false,
            isEngineer: false);

        Assert.Equal("Открыта", ticket.StatusName);
        Assert.Equal(creator.Id, ticket.CreatorId);
        Assert.Equal("High", ticket.Priority);
        Assert.True(await scope.Db.TicketAuditLogs.AnyAsync(log =>
            log.TicketId == ticket.Id && log.FieldName == "created"));
    }

    [Fact]
    public async Task CreateRepairTicketRequiresEngineerAndMovesAssetIntoRepair()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var asset = await CreateAssetAsync(scope.Db, AssetLifecycleStages.InStock);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(NewTicketRequest(typeId: 5, assetId: asset.Id), creator.Id, isStaff: true, isEngineer: false));

        var ticket = await service.CreateAsync(
            NewTicketRequest(typeId: 5, assetId: asset.Id),
            creator.Id,
            isStaff: true,
            isEngineer: true);

        Assert.Equal(asset.Id, ticket.AssetId);
        Assert.Equal("test", ticket.CustomFieldsData!.Value.GetProperty("source").GetString());
        Assert.Equal(AssetLifecycleStages.InRepair, (await scope.Db.Assets.FindAsync(asset.Id))!.LifecycleStage);
        Assert.Contains(await scope.Db.AssetHistoryLogs.Where(log => log.TicketId == ticket.Id).ToListAsync(),
            log => log.ActionType == AssetHistoryActionTypes.TicketCreated);
        Assert.Contains(await scope.Db.AssetHistoryLogs.Where(log => log.TicketId == ticket.Id).ToListAsync(),
            log => log.ActionType == AssetHistoryActionTypes.StageChanged
                && log.OldStatus == AssetLifecycleStages.InStock
                && log.NewStatus == AssetLifecycleStages.InRepair);
    }

    [Fact]
    public async Task RepairTicketCannotBeCreatedForAssetOutsideAllowedLifecycleStages()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var asset = await CreateAssetAsync(scope.Db, AssetLifecycleStages.Purchased);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(NewTicketRequest(typeId: 5, assetId: asset.Id), creator.Id, isStaff: true, isEngineer: true));
        Assert.Empty(await scope.Db.Tickets.ToListAsync());
    }

    [Fact]
    public async Task RepairTicketClosureUpdatesAssetAndRecordsResolution()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var engineer = await CreateUserAsync(scope.Db, "agent");
        var asset = await CreateAssetAsync(scope.Db, AssetLifecycleStages.InStock);
        var ticket = await service.CreateAsync(
            NewTicketRequest(typeId: 5, assetId: asset.Id),
            engineer.Id,
            isStaff: true,
            isEngineer: true);

        scope.Db.TicketStatusTransitions.Add(new TicketStatusTransition
        {
            FromStatusId = ticket.StatusId,
            ToStatusId = 5
        });
        await scope.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ChangeStatusAsync(
            ticket.Id,
            new ChangeTicketStatusRequest { StatusId = 5 },
            engineer.Id,
            ["agent"]));

        var closed = await service.ChangeStatusAsync(
            ticket.Id,
            new ChangeTicketStatusRequest
            {
                StatusId = 5,
                RepairResolution = "Заменен блок питания.",
                RepairOutcome = RepairOutcome.ReturnToExploitation
            },
            engineer.Id,
            ["agent"]);

        Assert.Equal("Закрыта", closed!.StatusName);
        Assert.Equal(AssetLifecycleStages.InUse, (await scope.Db.Assets.FindAsync(asset.Id))!.LifecycleStage);
        Assert.NotNull(await scope.Db.Tickets.Where(item => item.Id == ticket.Id)
            .Select(item => item.ActualResolutionTime).SingleAsync());
        Assert.Contains(await scope.Db.TicketMessages.Where(message => message.TicketId == ticket.Id).ToListAsync(),
            message => message.MessageBody == "Заменен блок питания." && message.SourceType == "system");
    }

    [Fact]
    public async Task TicketVisibilityAndMessagesAreRestrictedToCreatorOrStaff()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var otherUser = await CreateUserAsync(scope.Db);
        var agent = await CreateUserAsync(scope.Db, "agent");
        var ticket = await CreateTicketAsync(scope.Db, creator.Id);

        scope.Db.TicketMessages.AddRange(
            new TicketMessage
            {
                TicketId = ticket.Id,
                AuthorId = creator.Id,
                MessageBody = "Public reply",
                IsInternal = false
            },
            new TicketMessage
            {
                TicketId = ticket.Id,
                AuthorId = agent.Id,
                MessageBody = "Internal note",
                IsInternal = true
            });
        await scope.Db.SaveChangesAsync();

        Assert.Null(await service.GetByIdAsync(ticket.Id, otherUser.Id, isStaff: false));
        Assert.NotNull(await service.GetByIdAsync(ticket.Id, agent.Id, isStaff: true));
        Assert.Null(await service.GetMessagesAsync(ticket.Id, otherUser.Id, isStaff: false));
        Assert.Equal(
            new[] { "Public reply" },
            (await service.GetMessagesAsync(ticket.Id, creator.Id, isStaff: false))!.Select(message => message.MessageBody));
        Assert.Equal(
            2,
            (await service.GetMessagesAsync(ticket.Id, agent.Id, isStaff: true))!.Count);
    }

    [Fact]
    public async Task StatusTransitionRequiresConfiguredRoleAndWritesAudit()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var ticket = await CreateTicketAsync(scope.Db, creator.Id);
        var transition = await scope.Db.TicketStatusTransitions
            .SingleAsync(item => item.FromStatusId == 1 && item.ToStatusId == 2);
        transition.RequiredRoleCode = "agent";
        await scope.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ChangeStatusAsync(
                ticket.Id,
                new ChangeTicketStatusRequest { StatusId = 2 },
                creator.Id,
                ["portal_user"]));

        var updated = await service.ChangeStatusAsync(
            ticket.Id,
            new ChangeTicketStatusRequest { StatusId = 2 },
            creator.Id,
            ["agent"]);

        Assert.Equal(2, updated!.StatusId);
        Assert.True(await scope.Db.TicketAuditLogs.AnyAsync(log =>
            log.TicketId == ticket.Id
            && log.FieldName == "statusId"
            && log.OldValue == "1"
            && log.NewValue == "2"));
    }

    [Fact]
    public async Task AssignmentAcceptsOnlyActiveSupportEngineers()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var engineer = await CreateUserAsync(scope.Db, "agent");
        var portalUser = await CreateUserAsync(scope.Db);
        var ticket = await CreateTicketAsync(scope.Db, creator.Id);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AssignAsync(ticket.Id, new AssignTicketRequest { AssigneeId = portalUser.Id }, creator.Id));

        var assigned = await service.AssignAsync(
            ticket.Id,
            new AssignTicketRequest { AssigneeId = engineer.Id },
            creator.Id);

        Assert.Equal(engineer.Id, assigned!.AssigneeId);
        Assert.Equal(engineer.FullName, assigned.AssigneeName);
        Assert.True(await scope.Db.TicketAuditLogs.AnyAsync(log =>
            log.TicketId == ticket.Id && log.FieldName == "assigneeId"));
    }

    [Fact]
    public async Task UpdatePersistsPriorityCustomFieldsAndAssigneeRemoval()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var engineer = await CreateUserAsync(scope.Db, "agent");
        var ticket = await CreateTicketAsync(scope.Db, creator.Id);
        await service.AssignAsync(ticket.Id, new AssignTicketRequest { AssigneeId = engineer.Id }, creator.Id);

        var updated = await service.UpdateAsync(
            ticket.Id,
            new UpdateTicketRequest
            {
                Priority = "  Critical ",
                AssigneeId = JsonDocument.Parse("null").RootElement.Clone(),
                CustomFieldsData = JsonDocument.Parse("{\"impact\":\"department\"}").RootElement.Clone()
            },
            creator.Id);

        Assert.Equal("Critical", updated!.Priority);
        Assert.Null(updated.AssigneeId);
        Assert.Equal("department", updated.CustomFieldsData!.Value.GetProperty("impact").GetString());
        Assert.True(await scope.Db.TicketAuditLogs.AnyAsync(log =>
            log.TicketId == ticket.Id && log.FieldName == "priority"));
        Assert.True(await scope.Db.TicketAuditLogs.AnyAsync(log =>
            log.TicketId == ticket.Id && log.FieldName == "assigneeId"));
        Assert.True(await scope.Db.TicketAuditLogs.AnyAsync(log =>
            log.TicketId == ticket.Id && log.FieldName == "customFieldsData"));
    }

    [Fact]
    public async Task QueueReturnsNewestTicketsFirst()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var older = await CreateTicketAsync(scope.Db, creator.Id);
        older.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        await scope.Db.SaveChangesAsync();
        var newer = await CreateTicketAsync(scope.Db, creator.Id);

        var queue = await service.GetQueueAsync();
        var ticketIds = queue.Select(item => item.Id).ToList();

        Assert.True(ticketIds.IndexOf(newer.Id) < ticketIds.IndexOf(older.Id));
    }

    [Fact]
    public async Task AttachmentUploadCanOnlyBeConsumedOnceByItsUploaderForItsTicket()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var otherUser = await CreateUserAsync(scope.Db);
        var ticket = await CreateTicketAsync(scope.Db, creator.Id);
        var data = Encoding.UTF8.GetBytes("diagnostic details");
        await using var uploadStream = new MemoryStream(data);
        var upload = await service.UploadAttachmentAsync(
            ticket.Id,
            creator.Id,
            isStaff: false,
            @"C:\client\diagnostics.txt",
            uploadStream,
            data.Length);

        Assert.NotNull(upload);
        var request = new CreateTicketMessageRequest
        {
            MessageBody = "Прикладываю журнал диагностики.",
            Attachments = [upload!.Id]
        };
        var message = await service.CreateMessageAsync(ticket.Id, request, creator.Id, isStaff: false);

        var attachment = Assert.Single(message!.Attachments);
        Assert.Equal("diagnostics.txt", attachment.FileName);
        var file = await service.OpenAttachmentAsync(ticket.Id, attachment.Id, creator.Id, isStaff: false);
        Assert.NotNull(file);
        await using (file!.Content)
        using (var output = new MemoryStream())
        {
            await file.Content.CopyToAsync(output);
            Assert.Equal(data, output.ToArray());
        }

        Assert.Null(await service.OpenAttachmentAsync(ticket.Id, attachment.Id, otherUser.Id, isStaff: false));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateMessageAsync(ticket.Id, request, creator.Id, isStaff: false));
    }

    [Fact]
    public async Task PortalUserCannotCreateInternalMessagesOrAttachAnotherUsersUpload()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var otherUser = await CreateUserAsync(scope.Db);
        var ticket = await CreateTicketAsync(scope.Db, creator.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateMessageAsync(
                ticket.Id,
                new CreateTicketMessageRequest { MessageBody = "Internal", IsInternal = true },
                creator.Id,
                isStaff: false));

        var bytes = Encoding.UTF8.GetBytes("another user's file");
        await using var content = new MemoryStream(bytes);
        var upload = await service.UploadAttachmentAsync(
            ticket.Id,
            otherUser.Id,
            isStaff: true,
            "other.txt",
            content,
            bytes.Length);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateMessageAsync(
                ticket.Id,
                new CreateTicketMessageRequest { MessageBody = "Trying to attach", Attachments = [upload!.Id] },
                creator.Id,
                isStaff: false));
    }

    [Fact]
    public async Task ManualEscalationRaisesCriticalityAndNotifiesConfiguredHeadAndAdmins()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var head = await CreateUserAsync(scope.Db, "agent");
        var admin = await CreateUserAsync(scope.Db, "admin");
        var ticket = await CreateTicketAsync(scope.Db, creator.Id, criticalityLevelId: 1);
        await service.SetServiceDeskHeadAsync(new SetServiceDeskHeadRequest { UserId = head.Id });
        await service.SetEscalationRuleAsync(
            ticket.TypeId,
            new UpdateEscalationRuleRequest { OnSlaBreach = true, OnManual = true, IsActive = true });

        var escalation = await service.EscalateAsync(ticket.Id, creator.Id);

        Assert.Equal("OnManual", escalation!.Reason);
        Assert.Equal(1, escalation.FromCriticalityLevelId);
        Assert.Equal(2, escalation.ToCriticalityLevelId);
        Assert.Contains(head.Id, escalation.NotifiedUserIds);
        Assert.Contains(admin.Id, escalation.NotifiedUserIds);
        Assert.Equal(2, await scope.Db.Notifications.CountAsync(notification => notification.TicketId == ticket.Id));
        Assert.Equal(2, (await scope.Db.Tickets.FindAsync(ticket.Id))!.CriticalityLevelId);
    }

    [Fact]
    public async Task ManualEscalationIsRejectedWhenDisabledByRule()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var ticket = await CreateTicketAsync(scope.Db, creator.Id);
        await service.SetEscalationRuleAsync(
            ticket.TypeId,
            new UpdateEscalationRuleRequest { OnSlaBreach = true, OnManual = false, IsActive = true });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EscalateAsync(ticket.Id, creator.Id));
        Assert.Empty(await scope.Db.TicketEscalations.ToListAsync());
    }

    [Fact]
    public async Task EscalationRulesAndAssetHistoryReturnFilteredResults()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var engineer = await CreateUserAsync(scope.Db, "agent");
        var asset = await CreateAssetAsync(scope.Db, AssetLifecycleStages.InUse);
        var ticket = await CreateTicketAsync(scope.Db, creator.Id, asset.Id, criticalityLevelId: 2);
        await service.AssignAsync(ticket.Id, new AssignTicketRequest { AssigneeId = engineer.Id }, creator.Id);

        var defaults = await service.GetEscalationRulesAsync();
        Assert.Equal(5, defaults.Count);
        Assert.Contains(defaults, rule => rule.TicketTypeId == ticket.TypeId && rule.OnManual && rule.IsActive);

        await service.SetEscalationRuleAsync(
            ticket.TypeId,
            new UpdateEscalationRuleRequest { OnSlaBreach = false, OnManual = false, IsActive = false });
        var disabled = await service.GetEscalationRulesAsync();
        Assert.Contains(disabled, rule =>
            rule.TicketTypeId == ticket.TypeId && !rule.OnSlaBreach && !rule.OnManual && !rule.IsActive);

        var history = await service.GetAssetTicketHistoryAsync(
            asset.Id,
            typeId: 1,
            statusId: 1,
            criticalityLevelId: 2,
            assigneeId: engineer.Id,
            dateFrom: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            dateTo: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        var item = Assert.Single(history!);
        Assert.Equal(ticket.Id, item.Id);
        Assert.Equal(engineer.FullName, item.AssigneeName);
        Assert.Equal(2, item.CriticalityLevelId);
    }

    [Fact]
    public async Task ServiceDeskHeadCanBeReadAndReplaced()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var firstEngineer = await CreateUserAsync(scope.Db, "agent");
        var secondEngineer = await CreateUserAsync(scope.Db, "agent");

        var firstSetting = await service.SetServiceDeskHeadAsync(
            new SetServiceDeskHeadRequest { UserId = firstEngineer.Id });
        var current = await service.GetServiceDeskHeadAsync();
        var replaced = await service.SetServiceDeskHeadAsync(
            new SetServiceDeskHeadRequest { UserId = secondEngineer.Id });

        Assert.Equal(firstEngineer.Id, firstSetting!.UserId);
        Assert.Equal(firstEngineer.Username, current!.Login);
        Assert.Equal(secondEngineer.Id, replaced!.UserId);
        Assert.Equal(1, await scope.Db.ServiceDeskHeadSettings.CountAsync());
    }

    [Fact]
    public async Task ServiceDeskHeadMustBeActiveSupportEngineer()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var portalUser = await CreateUserAsync(scope.Db);
        var disabledEngineer = await CreateUserAsync(scope.Db, "agent", isActive: false);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SetServiceDeskHeadAsync(new SetServiceDeskHeadRequest { UserId = portalUser.Id }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SetServiceDeskHeadAsync(new SetServiceDeskHeadRequest { UserId = disabledEngineer.Id }));
        Assert.Null(await service.GetServiceDeskHeadAsync());
    }

    private static TicketService CreateService(TicketServiceTestScope scope)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:TicketAttachmentsPath"] = scope.StoragePath
            })
            .Build();
        return new TicketService(scope.Db, new PhysicalTicketAttachmentStorage(configuration));
    }

    private static async Task<User> CreateUserAsync(
        ItsmDbContext db,
        string? roleCode = null,
        bool isActive = true)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Username = $"user-{suffix}",
            Email = $"user-{suffix}@itsm.local",
            FullName = $"Test User {suffix[..8]}",
            TenantId = 1,
            IsActive = isActive,
            Status = "Verified"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        if (roleCode is not null)
        {
            var roleId = await db.SystemRoles
                .Where(role => role.Code == roleCode)
                .Select(role => role.Id)
                .SingleAsync();
            db.UserRoleMappings.Add(new UserRoleMapping { UserId = user.Id, RoleId = roleId });
            await db.SaveChangesAsync();
        }

        return user;
    }

    private static async Task<Asset> CreateAssetAsync(ItsmDbContext db, string stage)
    {
        var asset = new Asset
        {
            InventoryNumber = $"INV-{Guid.NewGuid():N}",
            Name = "Integration test asset",
            LifecycleStage = stage,
            TenantId = db.GetCurrentTenantId()
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        return asset;
    }

    private static async Task<Ticket> CreateTicketAsync(
        ItsmDbContext db,
        int creatorId,
        int? assetId = null,
        int? criticalityLevelId = null)
    {
        var ticket = new Ticket
        {
            Title = "Integration test ticket",
            Description = "Ticket created by an integration test.",
            TypeId = 1,
            StatusId = 1,
            Priority = "High",
            CreatorId = creatorId,
            TenantId = db.GetCurrentTenantId(),
            AssetId = assetId,
            CriticalityLevelId = criticalityLevelId
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return ticket;
    }

    private static CreateTicketRequest NewTicketRequest(int typeId = 1, int? assetId = null) =>
        new()
        {
            Title = "Integration test ticket",
            Description = "Ticket created by an integration test.",
            TypeId = typeId,
            Priority = "High",
            AssetId = assetId,
            CustomFieldsData = JsonDocument.Parse("{\"source\":\"test\"}").RootElement.Clone()
        };
}

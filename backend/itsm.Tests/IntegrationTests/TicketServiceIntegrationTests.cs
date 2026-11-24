using System.Text;
using System.Text.Json;
using System.Security.Claims;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using itsm.Infrastructure.Persistence.Services;
using itsm.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace itsm.Tests.IntegrationTests;

[Collection(TicketServiceTestCollection.Name)]
public sealed class TicketServiceIntegrationTests(TicketServiceFixture fixture)
{
    [Fact]
    public async Task MissingTemplateReturnsRequiredBuiltInFields()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var ticketType = await scope.Db.TicketTypes.OrderBy(type => type.Id).FirstAsync();
        var controller = CreateFormTemplatesController(scope.Db, "portal_user");

        var action = await controller.GetByTicketType(ticketType.Id, ct: CancellationToken.None);
        var response = Assert.IsType<FormTemplatesController.FormTemplateResponse>(
            Assert.IsType<OkObjectResult>(action.Result).Value);
        var fields = response.FieldsSchema.GetProperty("fields").EnumerateArray().ToArray();

        Assert.Equal(["subject", "description", "criticality"],
            fields.Select(field => field.GetProperty("id").GetString() ?? string.Empty).ToArray());
        Assert.All(fields, field => Assert.True(field.GetProperty("immutable").GetBoolean()));
    }

    [Fact]
    public async Task FormTemplateCanBeCreatedAndIsUsedWhenCreatingTickets()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var db = scope.Db;
        var ticketType = await db.TicketTypes.OrderBy(type => type.Id).FirstAsync();
        var controller = CreateFormTemplatesController(db);
        var commonSave = await controller.SaveCommon(new FormTemplatesController.SaveFormTemplateRequest
        {
            TicketTypeName = "Общие поля",
            Fields =
            [
                new FormTemplatesController.FormFieldSchemaRequest
                {
                    Id = "location",
                    Name = "Местоположение",
                    Type = "string",
                    Required = true,
                    VisibleToRequester = true
                }
            ]
        }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(commonSave.Result);

        var fields = RequiredBuiltInFields();
        fields.Add(new FormTemplatesController.FormFieldSchemaRequest
        {
            Id = "incident-code",
            Name = "Код инцидента",
            Type = "string",
            Required = false,
            VisibleToRequester = true
        });

        var save = await controller.Save(ticketType.Id, new FormTemplatesController.SaveFormTemplateRequest
        {
            TicketTypeId = ticketType.Id,
            TicketTypeName = ticketType.Name,
            Fields = fields
        }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(save.Result);
        Assert.True(await db.FormTemplates.AnyAsync(template => template.TicketTypeId == ticketType.Id));

        var creator = await CreateUserAsync(db);
        var request = NewTicketRequest(ticketType.Id) with
        {
            CustomFieldsData = JsonDocument.Parse("""{"location":"Москва"}""").RootElement.Clone()
        };
        var ticketService = CreateService(scope);
        var created = await ticketService.CreateAsync(request, creator.Id, isStaff: false, isEngineer: false);

        Assert.Equal("Москва", created.CustomFieldsData!.Value.GetProperty("location").GetString());
        var get = await controller.GetByTicketType(ticketType.Id, ct: CancellationToken.None);
        var response = Assert.IsType<FormTemplatesController.FormTemplateResponse>(
            Assert.IsType<OkObjectResult>(get.Result).Value);
        var schemaFields = response.FieldsSchema.GetProperty("fields").EnumerateArray().ToArray();
        Assert.Contains(schemaFields, field => field.GetProperty("id").GetString() == "location");
        Assert.Contains(schemaFields, field => field.GetProperty("id").GetString() == "incident-code");
    }

    [Fact]
    public async Task RemovingFormFieldClearsExistingTicketValues()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var db = scope.Db;
        var ticketType = await db.TicketTypes.OrderBy(type => type.Id).FirstAsync();
        var controller = CreateFormTemplatesController(db);
        var fields = RequiredBuiltInFields();
        fields.Add(new FormTemplatesController.FormFieldSchemaRequest
        {
            Id = "old-field",
            Name = "Старое поле",
            Type = "string",
            Required = false,
            VisibleToRequester = true
        });
        Assert.IsType<OkObjectResult>((await controller.Save(ticketType.Id, new()
        {
            TicketTypeId = ticketType.Id,
            TicketTypeName = ticketType.Name,
            Fields = fields
        }, CancellationToken.None)).Result);

        var creator = await CreateUserAsync(db);
        var ticket = await CreateService(scope).CreateAsync(NewTicketRequest(ticketType.Id) with
        {
            CustomFieldsData = JsonDocument.Parse("""{"old-field":"value"}""").RootElement.Clone()
        }, creator.Id, isStaff: false, isEngineer: false);
        fields.RemoveAt(fields.Count - 1);

        var save = await controller.Save(ticketType.Id, new()
        {
            TicketTypeId = ticketType.Id,
            TicketTypeName = ticketType.Name,
            Fields = fields
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(save.Result);
        var updatedTicket = await db.Tickets.SingleAsync(item => item.Id == ticket.Id);
        Assert.False(updatedTicket.CustomFieldsData!.Value.TryGetProperty("old-field", out _));
    }

    [Fact]
    public async Task AssetFieldCannotBeRemovedWhenAnExistingTicketUsesAnAsset()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var db = scope.Db;
        var ticketType = await db.TicketTypes.OrderBy(type => type.Id).FirstAsync();
        var controller = CreateFormTemplatesController(db);
        var fields = RequiredBuiltInFields();
        fields.Add(new FormTemplatesController.FormFieldSchemaRequest
        {
            Id = "linked-asset",
            Name = "Выбор актива",
            Type = "asset",
            Required = false,
            VisibleToRequester = true
        });
        Assert.IsType<OkObjectResult>((await controller.Save(ticketType.Id, new()
        {
            TicketTypeId = ticketType.Id,
            TicketTypeName = ticketType.Name,
            Fields = fields
        }, CancellationToken.None)).Result);

        var creator = await CreateUserAsync(db);
        var asset = await CreateAssetAsync(db, AssetLifecycleStages.InUse);
        await CreateService(scope).CreateAsync(NewTicketRequest(ticketType.Id, asset.Id) with
        {
            CustomFieldsData = JsonDocument.Parse($$"""{"linked-asset":"{{asset.Id}}"}""").RootElement.Clone()
        }, creator.Id, isStaff: false, isEngineer: false);
        fields.RemoveAt(fields.Count - 1);

        var save = await controller.Save(ticketType.Id, new()
        {
            TicketTypeId = ticketType.Id,
            TicketTypeName = ticketType.Name,
            Fields = fields
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(save.Result);
    }

    [Fact]
    public async Task DictionaryFieldUsesTenantDictionaryValuesForRenderingAndValidation()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var db = scope.Db;
        var ticketType = await db.TicketTypes.OrderBy(type => type.Id).FirstAsync();
        var dictionary = new itsm.Domain.Entities.Dictionary
        {
            TenantId = db.CurrentTenantId,
            Code = $"form-{Guid.NewGuid():N}",
            Name = "Локации"
        };
        dictionary.Values.Add(new DictionaryValue { Code = "moscow", Name = "Москва", SortOrder = 1 });
        db.Dictionaries.Add(dictionary);
        await db.SaveChangesAsync();

        var fields = RequiredBuiltInFields();
        fields.Add(new FormTemplatesController.FormFieldSchemaRequest
        {
            Id = "location",
            Name = "Местоположение",
            Type = "dictionary",
            Required = true,
            VisibleToRequester = true,
            DictionaryCode = dictionary.Code
        });
        var controller = CreateFormTemplatesController(db);
        var save = await controller.Save(ticketType.Id, new()
        {
            TicketTypeId = ticketType.Id,
            TicketTypeName = ticketType.Name,
            Fields = fields
        }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(save.Result);

        var get = await controller.GetByTicketType(ticketType.Id, ct: CancellationToken.None);
        var response = Assert.IsType<FormTemplatesController.FormTemplateResponse>(
            Assert.IsType<OkObjectResult>(get.Result).Value);
        var location = response.FieldsSchema.GetProperty("fields").EnumerateArray()
            .Single(field => field.GetProperty("id").GetString() == "location");
        Assert.Equal("Москва", location.GetProperty("options")[0].GetString());

        var creator = await CreateUserAsync(db);
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService(scope).CreateAsync(
            NewTicketRequest(ticketType.Id) with
            {
                CustomFieldsData = JsonDocument.Parse("""{"location":"Неизвестный город"}""").RootElement.Clone()
            },
            creator.Id,
            isStaff: false,
            isEngineer: false));
        var ticket = await CreateService(scope).CreateAsync(
            NewTicketRequest(ticketType.Id) with
            {
                CustomFieldsData = JsonDocument.Parse("""{"location":"Москва"}""").RootElement.Clone()
            },
            creator.Id,
            isStaff: false,
            isEngineer: false);
        Assert.Equal("Москва", ticket.CustomFieldsData!.Value.GetProperty("location").GetString());
    }

    [Fact]
    public async Task CreateTicketPersistsInitialStatusAndAudit()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        await service.UpdateAssignmentSettingAsync(new UpdateAssignmentSettingRequest { Mode = "manual" });
        var creator = await CreateUserAsync(scope.Db);

        var ticket = await service.CreateAsync(
            NewTicketRequest(),
            creator.Id,
            isStaff: false,
            isEngineer: false);

        Assert.Equal("Открыта", ticket.StatusName);
        Assert.Equal(creator.Id, ticket.CreatorId);
        Assert.Equal(creator.Id, ticket.RequesterId);
        Assert.Equal("High", ticket.Priority);
        Assert.Null(ticket.AssigneeId);
        Assert.True(await scope.Db.TicketAuditLogs.AnyAsync(log =>
            log.TicketId == ticket.Id && log.FieldName == "created"));
    }




    [Fact]
    public async Task CreateTicketByStaffRecordsSeparateRequester()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var agent = await CreateUserAsync(scope.Db, "agent");
        var requester = await CreateUserAsync(scope.Db);

        var ticket = await service.CreateAsync(
            NewTicketRequest(requesterId: requester.Id),
            agent.Id,
            isStaff: true,
            isEngineer: false);

        Assert.Equal(agent.Id, ticket.CreatorId);
        Assert.Equal(requester.Id, ticket.RequesterId);
        Assert.Equal(requester.FullName, ticket.RequesterName);
        Assert.True(await scope.Db.TicketAuditLogs.AnyAsync(log =>
            log.TicketId == ticket.Id && log.FieldName == "requesterId"));
    }

    [Fact]
    public async Task CreateTicketRejectsRequesterSelectionForPortalUser()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var portalUser = await CreateUserAsync(scope.Db);
        var otherUser = await CreateUserAsync(scope.Db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(
            NewTicketRequest(requesterId: otherUser.Id),
            portalUser.Id,
            isStaff: false,
            isEngineer: false));
    }

    [Fact]
    public async Task RequesterCanReadOwnTicketCreatedByStaff()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var agent = await CreateUserAsync(scope.Db, "agent");
        var requester = await CreateUserAsync(scope.Db);
        var outsider = await CreateUserAsync(scope.Db);
        var ticket = await service.CreateAsync(
            NewTicketRequest(requesterId: requester.Id),
            agent.Id,
            isStaff: true,
            isEngineer: false);

        Assert.NotNull(await service.GetByIdAsync(ticket.Id, requester.Id, isStaff: false));
        Assert.NotNull(await service.GetHistoryAsync(ticket.Id, requester.Id, isStaff: false));
        Assert.NotNull(await service.GetMessagesAsync(ticket.Id, requester.Id, isStaff: false));
        Assert.Null(await service.GetByIdAsync(ticket.Id, outsider.Id, isStaff: false));
        Assert.Null(await service.GetMessagesAsync(ticket.Id, outsider.Id, isStaff: false));
    }

    [Fact]
    public async Task GetHistoryReturnsTicketAuditAndRestrictsOtherUsers()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var otherUser = await CreateUserAsync(scope.Db);
        var ticket = await service.CreateAsync(
            NewTicketRequest(),
            creator.Id,
            isStaff: false,
            isEngineer: false);

        var history = await service.GetHistoryAsync(ticket.Id, creator.Id, isStaff: false);
        var unauthorizedHistory = await service.GetHistoryAsync(ticket.Id, otherUser.Id, isStaff: false);

        var entry = Assert.Single(history!);
        Assert.Equal("Заявка создана", entry.Event);
        Assert.Equal(creator.FullName, entry.Actor);
        Assert.Null(unauthorizedHistory);
    }

    [Fact]
    public async Task TicketHistoryPagesReturnNewestChangesFirst()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var ticket = await CreateTicketAsync(scope.Db, creator.Id);
        var changedAt = DateTimeOffset.UtcNow;

        scope.Db.TicketAuditLogs.AddRange(
            new TicketAuditLog
            {
                TicketId = ticket.Id,
                UserId = creator.Id,
                FieldName = "priority",
                OldValue = "Низкий",
                NewValue = "Средний",
                ChangedAt = changedAt.AddMinutes(-2)
            },
            new TicketAuditLog
            {
                TicketId = ticket.Id,
                UserId = creator.Id,
                FieldName = "priority",
                OldValue = "Средний",
                NewValue = "Высокий",
                ChangedAt = changedAt.AddMinutes(-1)
            });
        await scope.Db.SaveChangesAsync();

        var newest = await service.GetHistoryAsync(ticket.Id, creator.Id, isStaff: false, skip: 0, take: 1);
        var older = await service.GetHistoryAsync(ticket.Id, creator.Id, isStaff: false, skip: 1, take: 1);

        Assert.Equal("Приоритет изменен: Средний → Высокий", Assert.Single(newest!).Event);
        Assert.Equal("Приоритет изменен: Низкий → Средний", Assert.Single(older!).Event);
    }

    [Fact]
    public async Task TicketRequestsAreRestrictedToTheCurrentTenant()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var tenantOneCreator = await CreateUserAsync(scope.Db);
        var tenantOneTicket = await service.CreateAsync(
            NewTicketRequest(),
            tenantOneCreator.Id,
            isStaff: false,
            isEngineer: false);

        await CreateTenantAsync(scope.Db, 2);
        scope.Db.ChangeTracker.Clear();
        scope.Db.SetTenantId(2);
        var tenantTwoType = new TicketType
        {
            TenantId = 2,
            Code = "incident",
            Name = "Incident",
            IsPortalAvailable = true
        };
        var tenantTwoStatus = new TicketStatus
        {
            TenantId = 2,
            Code = "new",
            Name = "New"
        };
        scope.Db.TicketTypes.Add(tenantTwoType);
        scope.Db.TicketStatuses.Add(tenantTwoStatus);
        await scope.Db.SaveChangesAsync();
        var tenantTwoCreator = await CreateUserAsync(scope.Db);
        var tenantTwoTicket = await service.CreateAsync(
            NewTicketRequest(typeId: tenantTwoType.Id),
            tenantTwoCreator.Id,
            isStaff: false,
            isEngineer: false);

        Assert.Equal(2, await scope.Db.Tickets
            .Where(ticket => ticket.Id == tenantTwoTicket.Id)
            .Select(ticket => ticket.TenantId)
            .SingleAsync());

        var secondContextOptions = new DbContextOptionsBuilder<ItsmDbContext>()
            .UseNpgsql(scope.Db.Database.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        await using (var secondContext = new ItsmDbContext(secondContextOptions, new TenantContext()))
        {
            secondContext.SetTenantId(2);
            Assert.True(await secondContext.Tickets.AnyAsync(ticket => ticket.Id == tenantTwoTicket.Id));
            Assert.True(await secondContext.Users.AnyAsync(user => user.Id == tenantTwoCreator.Id));
            Assert.False(await secondContext.Users.AnyAsync(user => user.Id == tenantOneCreator.Id));
            secondContext.SetTenantId(1);
            Assert.False(await secondContext.Tickets.AnyAsync(ticket => ticket.Id == tenantTwoTicket.Id));
            Assert.True(await secondContext.Users.AnyAsync(user => user.Id == tenantOneCreator.Id));
            Assert.False(await secondContext.Users.AnyAsync(user => user.Id == tenantTwoCreator.Id));
        }

        scope.Db.ChangeTracker.Clear();
        scope.Db.SetTenantId(1);
        Assert.NotNull(await service.GetByIdAsync(tenantOneTicket.Id, tenantOneCreator.Id, isStaff: false));
        Assert.Null(await service.GetByIdAsync(tenantTwoTicket.Id, tenantOneCreator.Id, isStaff: true));
        Assert.Null(await service.UpdateAsync(
            tenantTwoTicket.Id,
            new UpdateTicketRequest { Priority = "Low" },
            tenantOneCreator.Id));
        Assert.Null(await service.GetMessagesAsync(tenantTwoTicket.Id, tenantOneCreator.Id, isStaff: true));
        Assert.Null(await service.AssignAsync(
            tenantTwoTicket.Id,
            new AssignTicketRequest { AssigneeId = tenantOneCreator.Id },
            tenantOneCreator.Id));
        Assert.Null(await service.EscalateAsync(tenantTwoTicket.Id, tenantOneCreator.Id));
        Assert.Single(await service.GetQueueAsync());
    }

    [Fact]
    public async Task ExpiredAttachmentCleanupOnlyRemovesCurrentTenantUploads()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var tenantOneCreator = await CreateUserAsync(scope.Db);
        var tenantOneTicket = await CreateTicketAsync(scope.Db, tenantOneCreator.Id);
        var tenantOneUploadId = await CreateExpiredUploadAsync(service, scope.Db, tenantOneTicket.Id, tenantOneCreator.Id);

        await CreateTenantAsync(scope.Db, 2);
        scope.Db.ChangeTracker.Clear();
        scope.Db.SetTenantId(2);
        var tenantTwoType = new TicketType
        {
            TenantId = 2,
            Code = "incident",
            Name = "Incident",
            IsPortalAvailable = true
        };
        var tenantTwoStatus = new TicketStatus
        {
            TenantId = 2,
            Code = "new",
            Name = "New"
        };
        scope.Db.TicketTypes.Add(tenantTwoType);
        scope.Db.TicketStatuses.Add(tenantTwoStatus);
        await scope.Db.SaveChangesAsync();
        var tenantTwoCreator = await CreateUserAsync(scope.Db);
        var tenantTwoTicket = await CreateTicketAsync(scope.Db, tenantTwoCreator.Id, typeId: tenantTwoType.Id, statusId: tenantTwoStatus.Id);
        var tenantTwoUploadId = await CreateExpiredUploadAsync(service, scope.Db, tenantTwoTicket.Id, tenantTwoCreator.Id);

        scope.Db.ChangeTracker.Clear();
        scope.Db.SetTenantId(1);
        var content = new MemoryStream(Encoding.UTF8.GetBytes("new upload"));
        await using (content)
            await service.UploadAttachmentAsync(
                tenantOneTicket.Id,
                tenantOneCreator.Id,
                isStaff: false,
                "new.txt",
                content,
                content.Length);

        Assert.False(await scope.Db.TicketAttachmentUploads.IgnoreQueryFilters()
            .AnyAsync(upload => upload.Id == tenantOneUploadId));
        Assert.True(await scope.Db.TicketAttachmentUploads.IgnoreQueryFilters()
            .AnyAsync(upload => upload.Id == tenantTwoUploadId));
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

        await Assert.ThrowsAsync<ArgumentException>(() => service.ChangeStatusAsync(
            ticket.Id,
            new ChangeTicketStatusRequest
            {
                StatusId = 5,
                RepairResolution = "Заменен блок питания.",
                RepairOutcome = RepairOutcome.ReturnToExploitation
            },
            engineer.Id,
            ["agent"]));

        var employee = await CreateUserAsync(scope.Db);
        var closed = await service.ChangeStatusAsync(
            ticket.Id,
            new ChangeTicketStatusRequest
            {
                StatusId = 5,
                RepairResolution = "Заменен блок питания.",
                RepairOutcome = RepairOutcome.ReturnToExploitation,
                RepairToUserId = employee.Id
            },
            engineer.Id,
            ["agent"]);

        Assert.Equal("Закрыта", closed!.StatusName);
        var updatedAsset = (await scope.Db.Assets.FindAsync(asset.Id))!;
        Assert.Equal(AssetLifecycleStages.InUse, updatedAsset.LifecycleStage);
        Assert.Equal(employee.Id, updatedAsset.AssignedUserId);
        Assert.Null(updatedAsset.WarehouseId);
        Assert.NotNull(await scope.Db.Tickets.Where(item => item.Id == ticket.Id)
            .Select(item => item.ActualResolutionTime).SingleAsync());
        Assert.Contains(await scope.Db.TicketMessages.Where(message => message.TicketId == ticket.Id).ToListAsync(),
            message => message.MessageBody == "Заменен блок питания." && message.SourceType == "system");
    }

    [Fact]
    public async Task RepairTicketClosureToWarehouseRequiresAndStoresWarehouse()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var engineer = await CreateUserAsync(scope.Db, "agent");
        var asset = await CreateAssetAsync(scope.Db, AssetLifecycleStages.InUse);
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

        var missingWarehouse = await Assert.ThrowsAsync<FieldValidationException>(() => service.ChangeStatusAsync(
            ticket.Id,
            new ChangeTicketStatusRequest
            {
                StatusId = 5,
                RepairResolution = "Ремонт завершен.",
                RepairOutcome = RepairOutcome.TransferToWarehouse
            },
            engineer.Id,
            ["agent"]));
        Assert.Equal(nameof(ChangeTicketStatusRequest.RepairToWarehouseCode), missingWarehouse.Field);

        var warehouseId = await CreateWarehouseAsync(scope.Db);
        var warehouseCode = await scope.Db.DictionaryValues
            .Where(value => value.Id == warehouseId)
            .Select(value => value.Code)
            .SingleAsync();
        await service.ChangeStatusAsync(
            ticket.Id,
            new ChangeTicketStatusRequest
            {
                StatusId = 5,
                RepairResolution = "Ремонт завершен.",
                RepairOutcome = RepairOutcome.TransferToWarehouse,
                RepairToWarehouseCode = warehouseCode
            },
            engineer.Id,
            ["agent"]);

        var updatedAsset = (await scope.Db.Assets.FindAsync(asset.Id))!;
        Assert.Equal(AssetLifecycleStages.InStock, updatedAsset.LifecycleStage);
        Assert.Equal(warehouseId, updatedAsset.WarehouseId);
        Assert.Null(updatedAsset.AssignedUserId);
    }

    [Fact]
    public async Task RepairTicketClosureReusesExistingAssigneeAndWarehouse()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var engineer = await CreateUserAsync(scope.Db, "agent");
        var inUseAsset = await CreateAssetAsync(scope.Db, AssetLifecycleStages.InUse);
        var existingAssigneeId = inUseAsset.AssignedUserId;
        var exploitationTicket = await service.CreateAsync(
            NewTicketRequest(typeId: 5, assetId: inUseAsset.Id),
            engineer.Id,
            isStaff: true,
            isEngineer: true);
        var inStockAsset = await CreateAssetAsync(scope.Db, AssetLifecycleStages.InStock);
        var existingWarehouseId = inStockAsset.WarehouseId;
        var warehouseTicket = await service.CreateAsync(
            NewTicketRequest(typeId: 5, assetId: inStockAsset.Id),
            engineer.Id,
            isStaff: true,
            isEngineer: true);

        scope.Db.TicketStatusTransitions.Add(new TicketStatusTransition
        {
            FromStatusId = exploitationTicket.StatusId,
            ToStatusId = 5
        });
        await scope.Db.SaveChangesAsync();

        await service.ChangeStatusAsync(
            exploitationTicket.Id,
            new ChangeTicketStatusRequest
            {
                StatusId = 5,
                RepairResolution = "Ремонт завершен.",
                RepairOutcome = RepairOutcome.ReturnToExploitation
            },
            engineer.Id,
            ["agent"]);
        await service.ChangeStatusAsync(
            warehouseTicket.Id,
            new ChangeTicketStatusRequest
            {
                StatusId = 5,
                RepairResolution = "Ремонт завершен.",
                RepairOutcome = RepairOutcome.TransferToWarehouse
            },
            engineer.Id,
            ["agent"]);

        var updatedInUseAsset = (await scope.Db.Assets.FindAsync(inUseAsset.Id))!;
        var updatedInStockAsset = (await scope.Db.Assets.FindAsync(inStockAsset.Id))!;
        Assert.Equal(AssetLifecycleStages.InUse, updatedInUseAsset.LifecycleStage);
        Assert.Equal(existingAssigneeId, updatedInUseAsset.AssignedUserId);
        Assert.Equal(AssetLifecycleStages.InStock, updatedInStockAsset.LifecycleStage);
        Assert.Equal(existingWarehouseId, updatedInStockAsset.WarehouseId);
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
                IsInternal = false,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            },
            new TicketMessage
            {
                TicketId = ticket.Id,
                AuthorId = agent.Id,
                MessageBody = "Internal note",
                IsInternal = true,
                CreatedAt = DateTimeOffset.UtcNow
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
        Assert.Equal(
            new[] { "Internal note" },
            (await service.GetMessagesAsync(ticket.Id, agent.Id, isStaff: true, skip: 0, take: 1))!.Select(message => message.MessageBody));
        Assert.Equal(
            new[] { "Public reply" },
            (await service.GetMessagesAsync(ticket.Id, creator.Id, isStaff: false, skip: 0, take: 1))!.Select(message => message.MessageBody));
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
        Assert.Equal("Эскалирована", (await service.GetByIdAsync(ticket.Id, creator.Id, isStaff: false))!.StatusName);
    }






    [Fact]
    public async Task EscalatedTicketIsListedAsPendingUntilAccepted()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var head = await CreateUserAsync(scope.Db, "agent");
        var ticket = await CreateTicketAsync(scope.Db, creator.Id, criticalityLevelId: 1);
        await service.SetServiceDeskHeadAsync(new SetServiceDeskHeadRequest { UserId = head.Id });

        await service.EscalateAsync(ticket.Id, creator.Id);

        var pending = await service.GetPendingEscalationsAsync();
        var item = Assert.Single(pending!);
        Assert.Equal(ticket.Id, item.TicketId);
        Assert.Null(item.AcceptedByUserId);
        Assert.Equal("Эскалирована", item.StatusName);
    }

    [Fact]
    public async Task ServiceDeskHeadAcceptsEscalationAndTicketMovesInProgress()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var head = await CreateUserAsync(scope.Db, "agent");
        var ticket = await CreateTicketAsync(scope.Db, creator.Id, criticalityLevelId: 1);
        await service.SetServiceDeskHeadAsync(new SetServiceDeskHeadRequest { UserId = head.Id });
        await service.EscalateAsync(ticket.Id, creator.Id);

        var accepted = await service.AcceptEscalationAsync(
            ticket.Id,
            new AcceptEscalationRequest { Comment = "Беру в работу" },
            head.Id,
            isAdmin: false);

        Assert.NotNull(accepted);
        Assert.Equal(head.Id, accepted!.AcceptedByUserId);
        Assert.Equal("Беру в работу", accepted.Comment);

        var updated = await service.GetByIdAsync(ticket.Id, head.Id, isStaff: true);
        Assert.Equal("В работе", updated!.StatusName);
        Assert.Equal(head.Id, updated.AssigneeId);

        Assert.Empty((await service.GetPendingEscalationsAsync())!);
        Assert.True(await scope.Db.Notifications.AnyAsync(notification =>
            notification.UserId == creator.Id && notification.Type == "TicketEscalationAccepted"));
        Assert.True(await scope.Db.TicketAuditLogs.AnyAsync(log =>
            log.TicketId == ticket.Id && log.FieldName == "escalationAccepted"));
    }

    [Fact]
    public async Task EscalationCannotBeAcceptedTwice()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var head = await CreateUserAsync(scope.Db, "agent");
        var ticket = await CreateTicketAsync(scope.Db, creator.Id, criticalityLevelId: 1);
        await service.SetServiceDeskHeadAsync(new SetServiceDeskHeadRequest { UserId = head.Id });
        await service.EscalateAsync(ticket.Id, creator.Id);
        await service.AcceptEscalationAsync(ticket.Id, new AcceptEscalationRequest(), head.Id, isAdmin: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptEscalationAsync(
            ticket.Id,
            new AcceptEscalationRequest(),
            head.Id,
            isAdmin: false));
    }

    [Fact]
    public async Task EscalationCanOnlyBeAcceptedByHeadOrAdmin()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var head = await CreateUserAsync(scope.Db, "agent");
        var otherAgent = await CreateUserAsync(scope.Db, "agent");
        var ticket = await CreateTicketAsync(scope.Db, creator.Id, criticalityLevelId: 1);
        await service.SetServiceDeskHeadAsync(new SetServiceDeskHeadRequest { UserId = head.Id });
        await service.EscalateAsync(ticket.Id, creator.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AcceptEscalationAsync(
            ticket.Id,
            new AcceptEscalationRequest(),
            otherAgent.Id,
            isAdmin: false));

        Assert.Single((await service.GetPendingEscalationsAsync())!);
    }

    [Fact]
    public async Task AdminWithoutAgentRoleCannotAcceptEscalation()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var head = await CreateUserAsync(scope.Db, "agent");
        var admin = await CreateUserAsync(scope.Db, "admin");
        var ticket = await CreateTicketAsync(scope.Db, creator.Id, criticalityLevelId: 1);
        await service.SetServiceDeskHeadAsync(new SetServiceDeskHeadRequest { UserId = head.Id });
        await service.EscalateAsync(ticket.Id, creator.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AcceptEscalationAsync(
            ticket.Id,
            new AcceptEscalationRequest(),
            admin.Id,
            isAdmin: true));
    }

    [Fact]
    public async Task ClosedTicketEscalationIsRejected()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var head = await CreateUserAsync(scope.Db, "agent");
        var ticket = await CreateTicketAsync(scope.Db, creator.Id, statusId: 5);
        await service.SetServiceDeskHeadAsync(new SetServiceDeskHeadRequest { UserId = head.Id });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EscalateAsync(ticket.Id, creator.Id));
        Assert.Empty(await scope.Db.TicketEscalations.ToListAsync());
    }

    [Fact]
    public async Task AutoAssignmentRespectsSpecializationGroup()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        await service.UpdateAssignmentSettingAsync(new UpdateAssignmentSettingRequest { Mode = "auto" });
        var creator = await CreateUserAsync(scope.Db);
        var groupOneAgent = await CreateUserAsync(scope.Db, "agent");
        var groupTwoAgent = await CreateUserAsync(scope.Db, "agent");

        var groupOne = new AgentGroup { TenantId = scope.Db.GetCurrentTenantId(), Code = "network", Name = "Сеть" };
        var groupTwo = new AgentGroup { TenantId = scope.Db.GetCurrentTenantId(), Code = "office", Name = "Офис" };
        scope.Db.AgentGroups.AddRange(groupOne, groupTwo);
        await scope.Db.SaveChangesAsync();
        scope.Db.AgentGroupMembers.AddRange(
            new AgentGroupMember { AgentGroupId = groupOne.Id, UserId = groupOneAgent.Id },
            new AgentGroupMember { AgentGroupId = groupTwo.Id, UserId = groupTwoAgent.Id });
        var ticketType = await scope.Db.TicketTypes.FirstAsync(type => type.Id == 2);
        ticketType.AssignmentGroupId = groupTwo.Id;
        await scope.Db.SaveChangesAsync();

        var ticket = await service.CreateAsync(
            NewTicketRequest(typeId: 2),
            creator.Id,
            isStaff: false,
            isEngineer: false);

        Assert.Equal(groupTwoAgent.Id, ticket.AssigneeId);
        Assert.Equal(groupTwo.Id, ticket.AssigneeGroupId);
    }

    [Fact]
    public async Task AutoAssignmentFallsBackWhenGroupHasNoMembers()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        await service.UpdateAssignmentSettingAsync(new UpdateAssignmentSettingRequest { Mode = "auto" });
        var creator = await CreateUserAsync(scope.Db);
        await CreateUserAsync(scope.Db, "agent");

        var emptyGroup = new AgentGroup { TenantId = scope.Db.GetCurrentTenantId(), Code = "empty", Name = "Пустая" };
        scope.Db.AgentGroups.Add(emptyGroup);
        await scope.Db.SaveChangesAsync();
        var ticketType = await scope.Db.TicketTypes.FirstAsync(type => type.Id == 2);
        ticketType.AssignmentGroupId = emptyGroup.Id;
        await scope.Db.SaveChangesAsync();

        var ticket = await service.CreateAsync(
            NewTicketRequest(typeId: 2),
            creator.Id,
            isStaff: false,
            isEngineer: false);

        Assert.NotNull(ticket.AssigneeId);
    }

    [Fact]
    public async Task ReassignUnavailableTicketsMovesOpenTicketsToAvailableAgent()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var leavingAgent = await CreateUserAsync(scope.Db, "agent");
        var remainingAgent = await CreateUserAsync(scope.Db, "agent");
        var ticket = await CreateTicketAsync(scope.Db, creator.Id, assigneeId: leavingAgent.Id);

        leavingAgent.IsActive = false;
        await scope.Db.SaveChangesAsync();
        await service.ReassignUnavailableTicketsAsync();

        var updated = await scope.Db.Tickets.FirstAsync(item => item.Id == ticket.Id);
        Assert.Equal(remainingAgent.Id, updated.AssigneeId);
        Assert.True(await scope.Db.TicketAuditLogs.AnyAsync(log =>
            log.TicketId == ticket.Id && log.FieldName == "autoAssigned" &&
            log.NewValue!.Contains("assignee-deactivated")));
    }

    [Fact]
    public async Task ReassignUnavailableTicketsSkipsClosedTickets()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var leavingAgent = await CreateUserAsync(scope.Db, "agent");
        await CreateUserAsync(scope.Db, "agent");
        var ticket = await CreateTicketAsync(scope.Db, creator.Id, assigneeId: leavingAgent.Id, statusId: 5);

        leavingAgent.IsActive = false;
        await scope.Db.SaveChangesAsync();
        await service.ReassignUnavailableTicketsAsync();

        var updated = await scope.Db.Tickets.FirstAsync(item => item.Id == ticket.Id);
        Assert.Equal(leavingAgent.Id, updated.AssigneeId);
    }

    [Fact]
    public async Task ManualAssignmentRequiresConfiguredAssigner()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var manager = await CreateUserAsync(scope.Db, "manager");
        var otherManager = await CreateUserAsync(scope.Db, "manager");
        var targetAgent = await CreateUserAsync(scope.Db, "agent");
        var ticket = await CreateTicketAsync(scope.Db, creator.Id);
        await service.UpdateAssignmentSettingAsync(new UpdateAssignmentSettingRequest
        {
            Mode = "manual",
            ManualAssignerUserId = manager.Id
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AssignAsync(
            ticket.Id,
            new AssignTicketRequest { AssigneeId = targetAgent.Id },
            otherManager.Id));

        var assigned = await service.AssignAsync(
            ticket.Id,
            new AssignTicketRequest { AssigneeId = targetAgent.Id },
            manager.Id);

        Assert.Equal(targetAgent.Id, assigned!.AssigneeId);
    }

    [Fact]
    public async Task AssignmentSettingRejectsNonManagerManualAssigner()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var agent = await CreateUserAsync(scope.Db, "agent");

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAssignmentSettingAsync(
            new UpdateAssignmentSettingRequest { Mode = "manual", ManualAssignerUserId = agent.Id }));
    }

    [Fact]
    public async Task AssignmentHistoryReturnsAutoAssignedEntriesWithFilters()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        await service.UpdateAssignmentSettingAsync(new UpdateAssignmentSettingRequest { Mode = "auto" });
        var creator = await CreateUserAsync(scope.Db);
        await CreateUserAsync(scope.Db, "agent");

        var ticket = await service.CreateAsync(
            NewTicketRequest(),
            creator.Id,
            isStaff: false,
            isEngineer: false);

        var history = await service.GetAssignmentHistoryAsync(ticket.AssigneeId, null, null);
        var entry = Assert.Single(history);
        Assert.Equal(ticket.Id, entry.TicketId);
        Assert.Equal(ticket.TicketNumber, entry.TicketNumber);
        Assert.Equal(ticket.AssigneeId, entry.NewAssigneeId);

        Assert.Empty(await service.GetAssignmentHistoryAsync(ticket.AssigneeId, DateTimeOffset.UtcNow.AddHours(1), null));
    }

    [Fact]
    public async Task AgentGroupsListReturnsMembersCount()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var agent = await CreateUserAsync(scope.Db, "agent");
        var group = new AgentGroup { TenantId = scope.Db.GetCurrentTenantId(), Code = "net", Name = "Сеть" };
        scope.Db.AgentGroups.Add(group);
        await scope.Db.SaveChangesAsync();
        scope.Db.AgentGroupMembers.Add(new AgentGroupMember { AgentGroupId = group.Id, UserId = agent.Id });
        await scope.Db.SaveChangesAsync();

        var groups = await service.GetAgentGroupsAsync();
        var option = Assert.Single(groups, item => item.Id == group.Id);
        Assert.Equal(1, option.MemberCount);
    }

    [Fact]
    public async Task AutoAssignmentOnCreatePicksLeastLoadedAgent()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        await service.UpdateAssignmentSettingAsync(new UpdateAssignmentSettingRequest { Mode = "auto" });
        var creator = await CreateUserAsync(scope.Db);
        var busyAgent = await CreateUserAsync(scope.Db, "agent");
        var freeAgent = await CreateUserAsync(scope.Db, "agent");
        await CreateTicketAsync(scope.Db, creator.Id, assigneeId: busyAgent.Id);

        var ticket = await service.CreateAsync(
            NewTicketRequest(),
            creator.Id,
            isStaff: false,
            isEngineer: false);

        Assert.Equal(freeAgent.Id, ticket.AssigneeId);
        Assert.True(await scope.Db.TicketAuditLogs.AnyAsync(log =>
            log.TicketId == ticket.Id && log.FieldName == "autoAssigned"));
    }

    [Fact]
    public async Task AutoAssignmentIsSkippedInManualMode()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        await CreateUserAsync(scope.Db, "agent");
        await service.UpdateAssignmentSettingAsync(new UpdateAssignmentSettingRequest { Mode = "manual" });

        var ticket = await service.CreateAsync(
            NewTicketRequest(),
            creator.Id,
            isStaff: false,
            isEngineer: false);

        Assert.Null(ticket.AssigneeId);
        Assert.False(await scope.Db.TicketAuditLogs.AnyAsync(log =>
            log.TicketId == ticket.Id && log.FieldName == "autoAssigned"));
    }

    [Fact]
    public async Task ManualAutoAssignEndpointReassignsToLeastLoadedAgent()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        await service.UpdateAssignmentSettingAsync(new UpdateAssignmentSettingRequest { Mode = "auto" });
        var creator = await CreateUserAsync(scope.Db);
        var busyAgent = await CreateUserAsync(scope.Db, "agent");
        var freeAgent = await CreateUserAsync(scope.Db, "agent");
        await CreateTicketAsync(scope.Db, creator.Id, assigneeId: busyAgent.Id);
        var target = await CreateTicketAsync(scope.Db, creator.Id, assigneeId: busyAgent.Id);

        var result = await service.AutoAssignAsync(target.Id, busyAgent.Id);

        Assert.NotNull(result);
        Assert.Equal(freeAgent.Id, result!.AssigneeId);
        Assert.Equal(0, result.OpenTicketsCount);

        var assigned = await scope.Db.Tickets.FindAsync(target.Id);
        Assert.Equal(freeAgent.Id, assigned!.AssigneeId);
    }

    [Fact]
    public async Task AutoAssignWithoutActiveAgentsThrows()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var creator = await CreateUserAsync(scope.Db);
        var ticket = await CreateTicketAsync(scope.Db, creator.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AutoAssignAsync(ticket.Id, creator.Id));
    }

    [Fact]
    public async Task AssignmentSettingIsPersistedAndValidated()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var defaults = await service.GetAssignmentSettingAsync();
        Assert.Equal("auto", defaults.Mode);

        var updated = await service.UpdateAssignmentSettingAsync(new UpdateAssignmentSettingRequest { Mode = "manual" });
        Assert.Equal("manual", updated.Mode);

        var reloaded = await service.GetAssignmentSettingAsync();
        Assert.Equal("manual", reloaded.Mode);
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
            search: null,
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
    public async Task ServiceDeskHeadSettingIsIsolatedByTenant()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var service = CreateService(scope);
        var tenantOneEngineer = await CreateUserAsync(scope.Db, "agent");
        await service.SetServiceDeskHeadAsync(new SetServiceDeskHeadRequest { UserId = tenantOneEngineer.Id });

        await CreateTenantAsync(scope.Db, 2);
        scope.Db.ChangeTracker.Clear();
        scope.Db.SetTenantId(2);
        var tenantTwoEngineer = await CreateUserAsync(scope.Db, "agent");
        await service.SetServiceDeskHeadAsync(new SetServiceDeskHeadRequest { UserId = tenantTwoEngineer.Id });
        Assert.Equal(2, await scope.Db.ServiceDeskHeadSettings.IgnoreQueryFilters().CountAsync());

        scope.Db.ChangeTracker.Clear();
        scope.Db.SetTenantId(1);
        Assert.Equal(tenantOneEngineer.Id, (await service.GetServiceDeskHeadAsync())!.UserId);
        scope.Db.ChangeTracker.Clear();
        scope.Db.SetTenantId(2);
        Assert.Equal(tenantTwoEngineer.Id, (await service.GetServiceDeskHeadAsync())!.UserId);
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

    private static FormTemplatesController CreateFormTemplatesController(ItsmDbContext db, string role = "admin") =>
        new(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Role, role)],
                        "test"))
                }
            }
        };

    private static List<FormTemplatesController.FormFieldSchemaRequest> RequiredBuiltInFields() =>
    [
        new()
        {
            Id = "subject",
            Name = "Тема",
            Type = "string",
            Required = true,
            VisibleToRequester = true,
            Immutable = true
        },
        new()
        {
            Id = "description",
            Name = "Описание",
            Type = "textarea",
            Required = true,
            VisibleToRequester = true,
            Immutable = true
        },
        new()
        {
            Id = "criticality",
            Name = "Критичность",
            Type = "dictionary",
            Required = true,
            VisibleToRequester = true,
            Immutable = true
        }
    ];

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
            TenantId = db.GetCurrentTenantId(),
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

    private static async Task CreateTenantAsync(ItsmDbContext db, int tenantId)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Code = $"tenant-{tenantId}",
            Name = $"Tenant {tenantId}"
        });
        await db.SaveChangesAsync();
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
        if (stage == AssetLifecycleStages.InStock)
            asset.WarehouseId = await CreateWarehouseAsync(db);
        else if (stage == AssetLifecycleStages.InUse)
            asset.AssignedUserId = (await CreateUserAsync(db)).Id;

        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        return asset;
    }

    private static async Task<int> CreateWarehouseAsync(ItsmDbContext db)
    {
        var tenantId = db.GetCurrentTenantId();
        var dictionary = await db.Dictionaries.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Code == "warehouses");
        if (dictionary is null)
        {
            dictionary = new itsm.Domain.Entities.Dictionary
            {
                TenantId = tenantId,
                Code = "warehouses",
                Name = "Склады",
                IsSystem = true
            };
            db.Dictionaries.Add(dictionary);
            await db.SaveChangesAsync();
        }

        var warehouse = new DictionaryValue
        {
            DictionaryId = dictionary.Id,
            Code = $"test-{Guid.NewGuid():N}",
            Name = "Тестовый склад"
        };
        db.DictionaryValues.Add(warehouse);
        await db.SaveChangesAsync();
        return warehouse.Id;
    }

    private static async Task<Ticket> CreateTicketAsync(
        ItsmDbContext db,
        int creatorId,
        int? assetId = null,
        int? criticalityLevelId = null,
        int typeId = 1,
        int statusId = 1,
        int? assigneeId = null)
    {
        var ticket = new Ticket
        {
            TicketNumber = TicketNumberGenerator.Generate(),
            Title = "Integration test ticket",
            Description = "Ticket created by an integration test.",
            TypeId = typeId,
            StatusId = statusId,
            Priority = "High",
            CreatorId = creatorId,
            TenantId = db.GetCurrentTenantId(),
            AssetId = assetId,
            AssigneeId = assigneeId,
            CriticalityLevelId = criticalityLevelId
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return ticket;
    }

    private static async Task<Guid> CreateExpiredUploadAsync(
        TicketService service,
        ItsmDbContext db,
        int ticketId,
        int userId)
    {
        var content = new MemoryStream(Encoding.UTF8.GetBytes("expired upload"));
        await using (content)
        {
            var upload = await service.UploadAttachmentAsync(
                ticketId,
                userId,
                isStaff: false,
                "expired.txt",
                content,
                content.Length);
            Assert.NotNull(upload);
            var uploadId = Guid.ParseExact(upload.Id, "N");
            var uploadRecord = await db.TicketAttachmentUploads.SingleAsync(item => item.Id == uploadId);
            uploadRecord.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
            return uploadId;
        }
    }

    private static CreateTicketRequest NewTicketRequest(int typeId = 1, int? assetId = null, int? requesterId = null) =>
        new()
        {
            Title = "Integration test ticket",
            Description = "Ticket created by an integration test.",
            TypeId = typeId,
            Priority = "High",
            AssetId = assetId,
            RequesterId = requesterId,
            CustomFieldsData = null
        };
}

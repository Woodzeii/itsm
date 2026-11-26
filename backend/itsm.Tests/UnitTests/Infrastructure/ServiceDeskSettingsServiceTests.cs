using FluentAssertions;
using itsm.Application.Common.Models;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using itsm.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;

namespace itsm.Tests.UnitTests.Infrastructure;

public sealed class ServiceDeskSettingsServiceTests
{
    [Fact]
    public async Task UpdateRejectsMoreThanFiveCriticalityLevels()
    {
        var (db, service) = await CreateAsync();
        await using var _ = db;
        var criticalities = Enumerable.Range(1, 6)
            .Select(index => new CriticalitySettingsItem
            {
                Id = index <= 3 ? index : 0,
                Name = $"Уровень {index}",
                SortOrder = index,
                IsActive = true
            })
            .ToArray();

        var act = () => service.UpdateAsync(
            new UpdateServiceDeskSettingsRequest { Criticalities = criticalities },
            canManageAdminCreation: true);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteCriticalityUsedByTicketIsRejected()
    {
        var (db, service) = await CreateAsync();
        await using var _ = db;
        db.Tickets.Add(new Ticket
        {
            Id = 100,
            TenantId = 1,
            TicketNumber = "INC-000-00100",
            Title = "Тестовая заявка",
            TypeId = 1,
            StatusId = 1,
            CriticalityLevelId = 2,
            CreatorId = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var act = () => service.DeleteCriticalityLevelAsync(2);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*назначен*");
    }

    [Fact]
    public async Task DeleteUnusedCriticalityRemovesLevelAndSlaValues()
    {
        var (db, service) = await CreateAsync();
        await using var _ = db;
        db.SlaValues.Add(new SlaValue { TicketTypeId = 1, CriticalityLevelId = 2, ReactionTimeMinutes = 30 });
        await db.SaveChangesAsync();

        await service.DeleteCriticalityLevelAsync(2);

        (await db.CriticalityLevels.AnyAsync(level => level.Id == 2)).Should().BeFalse();
        (await db.SlaValues.AnyAsync(value => value.CriticalityLevelId == 2)).Should().BeFalse();
    }

    [Fact]
    public async Task PasswordPolicyRejectsMinimumLengthBelowEight()
    {
        var (db, service) = await CreateAsync();
        await using var _ = db;

        var act = () => service.UpdatePasswordPolicyAsync(new UpdatePasswordPolicyRequest
        {
            MinLength = 7,
            MaxFailedAttempts = 5,
            LockoutMinutes = 15
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*8*");
    }

    [Fact]
    public async Task AggregateUpdatePersistsSettingsAndSlaMatrix()
    {
        var (db, service) = await CreateAsync();
        await using var _ = db;

        var result = await service.UpdateAsync(
            new UpdateServiceDeskSettingsRequest
            {
                AllowAdminsCreateAdmins = false,
                SlaEnabled = false,
                PortalSlaFields = new PortalSlaVisibilityDto { ShowReactionTime = true, ShowResolutionTime = false },
                PasswordPolicy = new UpdatePasswordPolicyRequest
                {
                    MinLength = 12,
                    MaxFailedAttempts = 4,
                    LockoutMinutes = 20,
                    ExpirationDays = 90
                },
                Criticalities =
                [
                    new CriticalitySettingsItem { Id = 1, Name = "Низкий", SortOrder = 1, IsActive = true },
                    new CriticalitySettingsItem { Id = 2, Name = "Средний", SortOrder = 2, IsActive = true },
                    new CriticalitySettingsItem { Id = 3, Name = "Высокий", SortOrder = 3, IsActive = true }
                ],
                TicketTypes =
                [
                    new TicketTypeSettingsItem
                    {
                        Id = 1,
                        Name = "Инцидент",
                        IsPortalAvailable = true,
                        IsSlaEnabled = true,
                        SlaValues =
                        [
                            new TicketTypeSlaValueItem
                            {
                                CriticalityName = "Высокий",
                                ReactionTimeMinutes = 30,
                                ResolutionTimeMinutes = 240
                            }
                        ]
                    }
                ]
            },
            canManageAdminCreation: true);

        result.AllowAdminsCreateAdmins.Should().BeFalse();
        result.SlaEnabled.Should().BeFalse();
        result.PortalSlaFields.ShowReactionTime.Should().BeTrue();
        result.PortalSlaFields.ShowResolutionTime.Should().BeFalse();
        result.PasswordPolicy.MinLength.Should().Be(12);
        result.TicketTypes.Single().SlaValues.Should().ContainSingle(value =>
            value.CriticalityLevelId == 3
            && value.ReactionTimeMinutes == 30
            && value.ResolutionTimeMinutes == 240);

        var tenant = await db.Tenants.SingleAsync();
        tenant.AllowAdminsCreateAdmins.Should().BeFalse();
        tenant.SlaEnabled.Should().BeFalse();
        tenant.PortalSlaReactionVisible.Should().BeTrue();
        tenant.PortalSlaResolutionVisible.Should().BeFalse();
    }

    [Fact]
    public async Task TicketTypeUsedByActiveTicketCannotBeDeleted()
    {
        var (db, service) = await CreateAsync();
        await using var _ = db;
        db.Tickets.Add(new Ticket
        {
            Id = 101,
            TenantId = 1,
            TicketNumber = "INC-000-00101",
            Title = "Активная заявка",
            TypeId = 1,
            StatusId = 1,
            CreatorId = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var act = () => service.DeleteTicketTypeAsync(1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*активных*");
    }

    [Fact]
    public async Task TicketTypeUsedOnlyByClosedTicketsIsArchived()
    {
        var (db, service) = await CreateAsync();
        await using var _ = db;
        db.Tickets.Add(new Ticket
        {
            Id = 102,
            TenantId = 1,
            TicketNumber = "INC-000-00102",
            Title = "Закрытая заявка",
            TypeId = 1,
            StatusId = 5,
            CreatorId = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        await service.DeleteTicketTypeAsync(1);

        (await db.TicketTypes.SingleAsync(type => type.Id == 1)).IsArchived.Should().BeTrue();
        (await service.GetTicketTypesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AggregateUpdatePersistsAssignmentModeAndRejectsInvalidAssigner()
    {
        var (db, service) = await CreateAsync();
        await using var _ = db;

        var act = () => service.UpdateAsync(
            new UpdateServiceDeskSettingsRequest
            {
                AssignmentMode = "manual",
                ManualAssignerUserId = 999
            },
            canManageAdminCreation: true);
        await act.Should().ThrowAsync<ArgumentException>();

        await service.UpdateAsync(
            new UpdateServiceDeskSettingsRequest
            {
                AssignmentMode = "manual",
                ManualAssignerUserId = 10
            },
            canManageAdminCreation: true);

        var setting = await db.AssignmentSettings.SingleAsync();
        setting.Mode.Should().Be("manual");
        setting.ManualAssignerUserId.Should().Be(10);
    }

    private static async Task<(ItsmDbContext Db, ServiceDeskSettingsService Service)> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<ItsmDbContext>()
            .UseInMemoryDatabase($"service-desk-settings-{Guid.NewGuid():N}")
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetCurrentTenantId(1);
        var db = new ItsmDbContext(options, tenantContext);

        db.Tenants.Add(new Tenant { Id = 1, Code = "default", Name = "Default Tenant" });
        db.TicketStatuses.AddRange(
            new TicketStatus { Id = 1, TenantId = 1, Code = "new", Name = "Открыта", SortOrder = 1 },
            new TicketStatus { Id = 5, TenantId = 1, Code = "closed", Name = "Закрыта", SortOrder = 5 });
        db.CriticalityLevels.AddRange(
            new CriticalityLevel { Id = 1, TenantId = 1, Code = "low", Name = "Низкий", SortOrder = 1 },
            new CriticalityLevel { Id = 2, TenantId = 1, Code = "medium", Name = "Средний", SortOrder = 2 },
            new CriticalityLevel { Id = 3, TenantId = 1, Code = "high", Name = "Высокий", SortOrder = 3 },
            new CriticalityLevel { Id = 4, TenantId = 1, Code = "critical", Name = "Критический", SortOrder = 4 });
        db.TicketTypes.Add(new TicketType { Id = 1, TenantId = 1, Code = "incident", Name = "Инцидент" });
        db.SystemRoles.AddRange(
            new SystemRole { Id = 2, Code = "admin", Name = "Администратор" },
            new SystemRole { Id = 4, Code = "manager", Name = "Руководитель" });
        db.Users.Add(new User
        {
            Id = 10,
            TenantId = 1,
            Username = "manager",
            Email = "manager@itsm.local",
            FullName = "Менеджер",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.UserRoleMappings.Add(new UserRoleMapping { UserId = 10, RoleId = 4 });
        await db.SaveChangesAsync();
        return (db, new ServiceDeskSettingsService(db));
    }
}
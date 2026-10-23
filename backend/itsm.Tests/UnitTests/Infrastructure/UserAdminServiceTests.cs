using FluentAssertions;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Application.Common.Validators;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using itsm.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace itsm.Tests.UnitTests.Infrastructure;

public class UserAdminServiceTests
{
    private const int AdminRoleId = 2;
    private const int AgentRoleId = 3;

    private static (UserAdminService svc, ItsmDbContext db) Create()
    {
        var options = new DbContextOptionsBuilder<ItsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ItsmDbContext(options);
        db.Database.EnsureCreated();

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(p => "hash:" + p);
        return (new UserAdminService(db, hasher.Object), db);
    }

    private static User AddUser(ItsmDbContext db, string name, bool admin = false, bool active = true)
    {
        var u = new User { Username = name, Email = name + "@x.local", FullName = name, IsActive = active };
        if (admin) u.RoleMappings.Add(new UserRoleMapping { RoleId = AdminRoleId });
        db.Users.Add(u);
        db.SaveChanges();
        return u;
    }

    [Fact]
    public async Task Create_HashesPasswordAssignsRolesAndAudits()
    {
        var (svc, db) = Create();
        var actor = AddUser(db, "actor", admin: true);

        var dto = await svc.CreateAsync(new CreateUserRequest
        {
            Username = "new.user", Email = "new@x.local", Password = "Passw0rdX",
            RoleIds = { AgentRoleId }, IsManager = true
        }, actor.Id, "1.2.3.4");

        dto.RoleIds.Should().Equal(AgentRoleId);
        dto.IsManager.Should().BeTrue();
        (await db.Users.SingleAsync(u => u.Id == dto.Id)).PasswordHash.Should().Be("hash:Passw0rdX");
        (await db.AuditLogs.SingleAsync(a => a.Action == "user_created")).IpAddress.Should().Be("1.2.3.4");
    }

    [Fact]
    public async Task Create_DuplicateUsername_Conflicts()
    {
        var (svc, db) = Create();
        var actor = AddUser(db, "actor", admin: true);

        var act = () => svc.CreateAsync(new CreateUserRequest
        { Username = "ACTOR", Email = "o@x.local", Password = "Passw0rdX" }, actor.Id, null);

        (await act.Should().ThrowAsync<UserAdminException>()).Which.Kind.Should().Be(UserAdminErrorKind.Conflict);
    }

    [Fact]
    public async Task ResetMfa_ClearsAllTwoFactorFields()
    {
        var (svc, db) = Create();
        var actor = AddUser(db, "actor", admin: true);
        var target = AddUser(db, "target");
        target.IsTwoFactorEnabled = true;
        target.TwoFaSecret = "SECRET";
        target.TwoFactorEnabledAt = DateTimeOffset.UtcNow;
        db.SaveChanges();

        await svc.ResetMfaAsync(target.Id, actor.Id, null);

        var reloaded = await db.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
        reloaded.IsTwoFactorEnabled.Should().BeFalse();
        reloaded.TwoFaSecret.Should().BeNull();
        reloaded.TwoFactorEnabledAt.Should().BeNull();
        (await db.AuditLogs.CountAsync(a => a.Action == "user_mfa_reset")).Should().Be(1);
    }

    [Fact]
    public async Task Delete_IsSoft_AndSelfDeleteRejected()
    {
        var (svc, db) = Create();
        var actor = AddUser(db, "actor", admin: true);
        var other = AddUser(db, "other");

        await svc.DeleteAsync(other.Id, actor.Id, null);
        (await db.Users.SingleAsync(u => u.Id == other.Id)).IsActive.Should().BeFalse();

        var self = () => svc.DeleteAsync(actor.Id, actor.Id, null);
        (await self.Should().ThrowAsync<UserAdminException>()).Which.Kind.Should().Be(UserAdminErrorKind.Conflict);
    }

    [Fact]
    public async Task Deactivate_LastAdmin_Rejected()
    {
        var (svc, db) = Create();
        var actor = AddUser(db, "actor");
        var admin = AddUser(db, "admin", admin: true);

        var act = () => svc.SetActiveAsync(admin.Id, false, actor.Id, null);
        (await act.Should().ThrowAsync<UserAdminException>()).Which.Kind.Should().Be(UserAdminErrorKind.Conflict);

        AddUser(db, "admin2", admin: true);
        (await svc.SetActiveAsync(admin.Id, false, actor.Id, null)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SetRoles_ReplacesRolesAndFlag_ProtectsLastAdminAndSelf()
    {
        var (svc, db) = Create();
        var actor = AddUser(db, "actor", admin: true);
        var target = AddUser(db, "target");

        var dto = await svc.SetRolesAsync(target.Id,
            new SetUserRolesRequest { RoleIds = { AgentRoleId }, IsManager = true }, actor.Id, null);
        dto.RoleIds.Should().Equal(AgentRoleId);
        dto.IsManager.Should().BeTrue();

        var self = () => svc.SetRolesAsync(actor.Id, new SetUserRolesRequest(), actor.Id, null);
        (await self.Should().ThrowAsync<UserAdminException>()).Which.Kind.Should().Be(UserAdminErrorKind.Conflict);

        var byCode = await svc.SetRolesAsync(target.Id,
            new SetUserRolesRequest { RoleCodes = { "agent" }, IsManager = true }, actor.Id, null);
        byCode.RoleCodes.Should().Equal("agent");

        var selfDeactivate = () => svc.SetActiveAsync(actor.Id, false, actor.Id, null);
        (await selfDeactivate.Should().ThrowAsync<UserAdminException>()).Which.Kind.Should().Be(UserAdminErrorKind.Conflict);

        var bad = () => svc.SetRolesAsync(target.Id, new SetUserRolesRequest { RoleIds = { 9999 } }, actor.Id, null);
        (await bad.Should().ThrowAsync<UserAdminException>()).Which.Kind.Should().Be(UserAdminErrorKind.Validation);
    }

    [Fact]
    public async Task SetRoles_RemovingAdminFromLastAdmin_Rejected()
    {
        var (svc, db) = Create();
        var actor = AddUser(db, "actor");
        var admin = AddUser(db, "admin", admin: true);

        var act = () => svc.SetRolesAsync(admin.Id, new SetUserRolesRequest(), actor.Id, null);
        (await act.Should().ThrowAsync<UserAdminException>()).Which.Kind.Should().Be(UserAdminErrorKind.Conflict);
    }

    [Fact]
    public async Task Unlock_ResetsCounters_And_List_Paginates()
    {
        var (svc, db) = Create();
        var actor = AddUser(db, "actor", admin: true);
        var target = AddUser(db, "target", active: false);
        target.FailedLoginAttempts = 5;
        target.LockedUntil = DateTimeOffset.UtcNow.AddHours(1);
        db.SaveChanges();

        var dto = await svc.UnlockAsync(target.Id, actor.Id, null);
        dto.FailedLoginAttempts.Should().Be(0);
        dto.LockedUntil.Should().BeNull();

        var page = await svc.GetListAsync(new UserListQuery { IsActive = true, PageSize = 1 });
        page.Total.Should().Be(1);
        page.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task NotFound_ForMissingUser()
    {
        var (svc, _) = Create();
        var act = () => svc.ResetMfaAsync(404, 1, null);
        (await act.Should().ThrowAsync<UserAdminException>()).Which.Kind.Should().Be(UserAdminErrorKind.NotFound);
        (await svc.GetByIdAsync(404)).Should().BeNull();
    }

    [Fact]
    public void Validators_RejectBadInput()
    {
        new CreateUserRequestValidator().Validate(new CreateUserRequest
        { Username = "a", Email = "bad", Password = "short" }).IsValid.Should().BeFalse();
        new CreateUserRequestValidator().Validate(new CreateUserRequest
        { Username = "good.user", Email = "g@x.io", Password = "Passw0rdX" }).IsValid.Should().BeTrue();
        new UserListQueryValidator().Validate(new UserListQuery { PageSize = 0 }).IsValid.Should().BeFalse();
    }
}

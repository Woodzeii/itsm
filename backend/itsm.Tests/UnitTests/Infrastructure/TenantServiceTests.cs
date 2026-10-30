using FluentAssertions;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using itsm.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace itsm.Tests.UnitTests.Infrastructure;

public class TenantServiceTests
{
    private static (TenantService service, ItsmDbContext db) Create()
    {
        var options = new DbContextOptionsBuilder<ItsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetCurrentTenantId(1);
        var db = new ItsmDbContext(options, tenantContext);
        db.Database.EnsureCreated();

        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(hasher => hasher.Hash(It.IsAny<string>()))
            .Returns<string>(password => $"hashed:{password}");

        return (new TenantService(db, passwordHasher.Object), db);
    }

    [Fact]
    public async Task Create_CreatesTenantAndVerifiedAdminWithHashedPassword()
    {
        var (service, db) = Create();

        var tenant = await service.CreateAsync(new CreateTenantRequest
        {
            Name = "Example Company",
            FirstAdminLogin = "company.admin",
            FirstAdminEmail = "admin@example.test",
            FirstAdminFullName = "Company Administrator",
            FirstAdminPassword = "Passw0rd1"
        });

        tenant.Code.Should().Be("example-company");
        tenant.CurrentUserCount.Should().Be(1);

        var admin = await db.Users.IgnoreQueryFilters()
            .Include(user => user.RoleMappings)
            .ThenInclude(mapping => mapping.Role)
            .SingleAsync(user => user.TenantId == tenant.Id);
        admin.Username.Should().Be("company.admin");
        admin.Email.Should().Be("admin@example.test");
        admin.FullName.Should().Be("Company Administrator");
        admin.Status.Should().Be(UserStatuses.Verified);
        admin.IsActive.Should().BeTrue();
        admin.PasswordHash.Should().Be("hashed:Passw0rd1");
        admin.RoleMappings.Select(mapping => mapping.Role.Code).Should().Contain("admin");
    }

    [Fact]
    public async Task Delete_RejectsUsersOutsideCurrentTenantFilter()
    {
        var (service, db) = Create();
        var tenant = new Tenant { Id = 2, Code = "other", Name = "Other Tenant" };
        db.Tenants.Add(tenant);
        db.Users.Add(new User
        {
            TenantId = tenant.Id,
            Username = "other.admin",
            Email = "admin@other.test",
            Tenant = tenant
        });
        await db.SaveChangesAsync();

        var act = () => service.DeleteAsync(tenant.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Нельзя удалить тенант с пользователями*");
    }

    [Fact]
    public async Task Create_RejectsDuplicateNormalizedTenantCode()
    {
        var (service, _) = Create();
        await service.CreateAsync(new CreateTenantRequest
        {
            Name = "Example Company",
            FirstAdminLogin = "first.admin",
            FirstAdminEmail = "first@example.test",
            FirstAdminFullName = "First Admin",
            FirstAdminPassword = "Passw0rd1"
        });

        var act = () => service.CreateAsync(new CreateTenantRequest
        {
            Name = "Example_Company",
            FirstAdminLogin = "second.admin",
            FirstAdminEmail = "second@example.test",
            FirstAdminFullName = "Second Admin",
            FirstAdminPassword = "Passw0rd1"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*уже существует*");
    }
}

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

    [Fact]
    public async Task GetAll_ReportsActualActiveUsersPerTenantInsteadOfStoredCounter()
    {
        var (service, db) = Create();
        var defaultTenant = await db.Tenants.SingleAsync(tenant => tenant.Id == 1);
        defaultTenant.CurrentUserCount = 0;
        var secondTenant = new Tenant { Code = "second", Name = "Second Tenant" };
        db.Tenants.Add(secondTenant);

        var seededUsers = new List<User>();
        for (var index = 0; index < 7; index++)
        {
            seededUsers.Add(new User
            {
                Username = $"seed.user.{index}",
                Email = $"seed.user.{index}@example.test",
                TenantId = defaultTenant.Id,
                IsActive = true
            });
        }

        db.Users.AddRange(seededUsers);
        db.Users.Add(new User
        {
            Username = "second.tenant.user",
            Email = "second@example.test",
            TenantId = secondTenant.Id,
            IsActive = true
        });
        db.Users.Add(new User
        {
            Username = "inactive.seed.user",
            Email = "inactive@example.test",
            TenantId = defaultTenant.Id,
            IsActive = false
        });
        await db.SaveChangesAsync();

        db.Assets.Add(new Asset
        {
            TenantId = defaultTenant.Id,
            InventoryNumber = "seed-asset-1",
            Name = "Seeded Asset"
        });
        var ticket = new Ticket
        {
            TenantId = defaultTenant.Id,
            Title = "Seeded Ticket",
            TypeId = 1,
            StatusId = 1,
            CreatorId = seededUsers[0].Id
        };
        var message = new TicketMessage
        {
            Ticket = ticket,
            AuthorId = seededUsers[0].Id,
            MessageBody = "Seeded attachment"
        };
        message.Attachments.Add(new Attachment
        {
            FileName = "seed.txt",
            FilePath = "seed.txt",
            ContentType = "text/plain",
            SizeBytes = 123,
            UploadedById = seededUsers[0].Id,
            UploadedBy = seededUsers[0]
        });
        db.Tickets.Add(ticket);
        db.TicketMessages.Add(message);
        await db.SaveChangesAsync();

        var tenants = await service.GetAllAsync();

        var defaultTenantDto = tenants.Single(tenant => tenant.Id == defaultTenant.Id);
        defaultTenantDto.CurrentUserCount.Should().Be(7);
        defaultTenantDto.CurrentAssetCount.Should().Be(1);
        defaultTenantDto.CurrentTicketCount.Should().Be(1);
        defaultTenantDto.CurrentStorageBytes.Should().Be(123);
        tenants.Single(tenant => tenant.Id == secondTenant.Id).CurrentUserCount.Should().Be(1);
    }
}

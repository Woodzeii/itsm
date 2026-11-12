using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace itsm.Tests.UnitTests.Infrastructure;

public class ItsmDbContextTests
{
    [Fact]
    public void Model_ContainsSchemaTablesKeysAndJsonbColumns()
    {
        var options = new DbContextOptionsBuilder<ItsmDbContext>()
            .UseNpgsql("Host=localhost;Database=itsm;Username=itsm;Password=itsm")
            .Options;

        using var context = new ItsmDbContext(options);
        var model = context.Model;

        Assert.Equal(42, model.GetEntityTypes().Count());
        Assert.Equal(
            new[] { nameof(TicketAssetMapping.TicketId), nameof(TicketAssetMapping.AssetId) },
            model.FindEntityType(typeof(TicketAssetMapping))!.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.Equal(
            "jsonb",
            model.FindEntityType(typeof(Ticket))!.FindProperty(nameof(Ticket.CustomFieldsData))!.GetColumnType());
    }

    [Fact]
    public async Task TenantQueryFilter_UsesTenantOfEachDbContextInstance()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ItsmDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var tenantOneContext = new TenantContext();
        tenantOneContext.SetCurrentTenantId(1);
        await using (var tenantOneDb = new ItsmDbContext(options, tenantOneContext))
        {
            await tenantOneDb.Database.EnsureCreatedAsync();
            tenantOneDb.Users.AddRange(
                new User { Username = "tenant-one", Email = "one@example.test", TenantId = 1 },
                new User { Username = "tenant-three", Email = "three@example.test", TenantId = 3 });
            await tenantOneDb.SaveChangesAsync();
            Assert.Equal(1, await tenantOneDb.Users.CountAsync());
        }

        var tenantThreeContext = new TenantContext();
        tenantThreeContext.SetCurrentTenantId(3);
        await using var tenantThreeDb = new ItsmDbContext(options, tenantThreeContext);

        var users = await tenantThreeDb.Users.ToListAsync();

        Assert.Equal(new[] { "tenant-three" }, users.Select(user => user.Username));
    }
}
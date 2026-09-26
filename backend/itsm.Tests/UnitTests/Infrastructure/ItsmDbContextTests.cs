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

        Assert.Equal(19, model.GetEntityTypes().Count());
        Assert.Equal(
            new[] { nameof(TicketAssetMapping.TicketId), nameof(TicketAssetMapping.AssetId) },
            model.FindEntityType(typeof(TicketAssetMapping))!.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.Equal(
            "jsonb",
            model.FindEntityType(typeof(Ticket))!.FindProperty(nameof(Ticket.CustomFieldsData))!.GetColumnType());
    }
}
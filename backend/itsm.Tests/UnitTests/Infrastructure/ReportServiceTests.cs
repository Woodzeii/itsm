using FluentAssertions;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using itsm.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;

namespace itsm.Tests.UnitTests.Infrastructure;

public class ReportServiceTests
{
    [Fact]
    public async Task GetSummariesAndEngineerDetails_UseDatabaseRowsWithinPeriodAndExcludeClosedTicketsFromLoad()
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
        summaries.Single(report => report.Id == "engineer-load").Count.Should().Be(2);
        engineerLoad.Should().NotBeNull();
        engineerLoad!.Total.Should().Be(1);
        engineerLoad.Items.Single().Values["assignee"].Should().Be("Инженер");
        engineerLoad.Items.Single().Values["count"].Should().Be("2");
    }

    private static Ticket CreateTicket(
        string number,
        int creatorId,
        int? assigneeId,
        int statusId,
        DateTimeOffset createdAt) => new()
    {
        TenantId = 1,
        TicketNumber = number,
        Title = number,
        TypeId = 1,
        StatusId = statusId,
        CreatorId = creatorId,
        AssigneeId = assigneeId,
        CreatedAt = createdAt,
    };
}

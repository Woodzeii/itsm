using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace itsm.Infrastructure.Persistence;

public sealed class ItsmDbContextFactory : IDesignTimeDbContextFactory<ItsmDbContext>
{
    public ItsmDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=itsm;Username=itsm;Password=itsm";

        var options = new DbContextOptionsBuilder<ItsmDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ItsmDbContext(options);
    }
}
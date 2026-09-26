using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace itsm.Infrastructure.Persistence;

public sealed class ItsmDbContextFactory : IDesignTimeDbContextFactory<ItsmDbContext>
{
    public ItsmDbContext CreateDbContext(string[] args)
    {
        DotEnvLoader.LoadIfPresent();
        var connectionString = DatabaseConfiguration.GetConnectionString(Environment.GetEnvironmentVariable);

        var options = new DbContextOptionsBuilder<ItsmDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ItsmDbContext(options);
    }
}
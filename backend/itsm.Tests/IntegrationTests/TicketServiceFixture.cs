using itsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace itsm.Tests.IntegrationTests;

public sealed class TicketServiceFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("itsm_ticket_tests")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public async Task InitializeAsync() =>
        await _database.StartAsync();

    public async Task DisposeAsync() =>
        await _database.DisposeAsync();

    public async Task<TicketServiceTestScope> CreateScopeAsync()
    {
        var databaseName = $"ticket_test_{Guid.NewGuid():N}";
        var adminConnectionString = new NpgsqlConnectionStringBuilder(_database.GetConnectionString())
        {
            Database = "postgres"
        };

        await using (var connection = new NpgsqlConnection(adminConnectionString.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await command.ExecuteNonQueryAsync();
        }

        var databaseConnectionString = new NpgsqlConnectionStringBuilder(_database.GetConnectionString())
        {
            Database = databaseName
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<ItsmDbContext>()
            .UseNpgsql(databaseConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        var db = new ItsmDbContext(options);
        await db.Database.MigrateAsync();

        var storagePath = Path.Combine(Path.GetTempPath(), $"itsm-ticket-test-{Guid.NewGuid():N}");
        return new TicketServiceTestScope(db, databaseName, adminConnectionString.ConnectionString, storagePath);
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TicketServiceTestCollection : ICollectionFixture<TicketServiceFixture>
{
    public const string Name = "Ticket service integration";
}

public sealed class TicketServiceTestScope(
    ItsmDbContext db,
    string databaseName,
    string adminConnectionString,
    string storagePath) : IAsyncDisposable
{
    public ItsmDbContext Db { get; } = db;
    public string StoragePath { get; } = storagePath;

    public ValueTask DisposeAsync() => DisposeCoreAsync();

    private async ValueTask DisposeCoreAsync()
    {
        await Db.DisposeAsync();

        await using (var connection = new NpgsqlConnection(adminConnectionString))
        {
            await connection.OpenAsync();
            await using var terminateCommand = connection.CreateCommand();
            terminateCommand.CommandText = "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @databaseName";
            terminateCommand.Parameters.AddWithValue("databaseName", databaseName);
            await terminateCommand.ExecuteNonQueryAsync();

            await using var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = $"DROP DATABASE \"{databaseName}\"";
            await dropCommand.ExecuteNonQueryAsync();
        }

        if (Directory.Exists(StoragePath))
            Directory.Delete(StoragePath, recursive: true);
    }
}

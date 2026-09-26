using Npgsql;

namespace itsm.Infrastructure.Persistence;

internal static class DatabaseConfiguration
{
    public static string GetConnectionString(Func<string, string?> getValue)
    {
        var configuredConnectionString = getValue("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
            return configuredConnectionString;

        var database = getValue("POSTGRES_DB");
        var username = getValue("POSTGRES_USER");
        var password = getValue("POSTGRES_PASSWORD");
        if (string.IsNullOrWhiteSpace(database)
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Database configuration is missing. Set ConnectionStrings__DefaultConnection " +
                "or POSTGRES_DB, POSTGRES_USER, and POSTGRES_PASSWORD in the .env file.");
        }

        var port = int.TryParse(getValue("POSTGRES_PORT"), out var configuredPort)
            ? configuredPort
            : 5432;

        return new NpgsqlConnectionStringBuilder
        {
            Host = getValue("POSTGRES_HOST") ?? "localhost",
            Port = port,
            Database = database,
            Username = username,
            Password = password
        }.ConnectionString;
    }
}
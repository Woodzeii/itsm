namespace itsm.Application.Common.Interfaces;

public interface IDatabaseSeeder
{
    Task SeedAsync(CancellationToken ct = default);
    Task SeedProductionAsync(
        string? tenantAdminUsername, string? tenantAdminEmail, string? tenantAdminPassword,
        string? adminUsername, string? adminEmail, string? adminPassword,
        CancellationToken ct = default);
}
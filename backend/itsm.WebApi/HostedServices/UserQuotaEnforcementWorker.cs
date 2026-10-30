using itsm.Infrastructure.Persistence.Services;

namespace itsm.WebApi.HostedServices;

public sealed class UserQuotaEnforcementWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<UserQuotaEnforcementWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnforceExpiredLimitsAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await EnforceExpiredLimitsAsync(stoppingToken);
    }

    private async Task EnforceExpiredLimitsAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
            await tenantService.EnforceExpiredUserLimitsAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to enforce expired tenant user limits");
        }
    }
}

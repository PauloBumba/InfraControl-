using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InfraControl.Workers;

public sealed class CleanupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<CleanupWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    private readonly bool _enabled = configuration.GetValue("Cleanup:Enabled", true);
    private readonly TimeSpan _interval = TimeSpan.FromHours(
        Math.Max(1, configuration.GetValue("Cleanup:IntervalHours", 24)));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            logger.LogInformation("Cleanup worker is disabled via Cleanup:Enabled=false.");
            return;
        }

        logger.LogInformation("Cleanup worker started with interval {Interval}.", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Cleanup worker run failed.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var cleanup = scope.ServiceProvider.GetRequiredService<ICleanupService>();
        var utcNow = AppTimeProvider.Current.GetUtcNow().UtcDateTime;

        var result = await cleanup.CleanupAsync(utcNow, cancellationToken);

        logger.LogInformation(
            "Cleanup completed. Outbox={OutboxDeleted} Processed={ProcessedDeleted} Audit={AuditDeleted} " +
            "Monitoring={MonitoringDeleted} Notifications={NotificationDeleted} " +
            "RemoteSessions={RemoteSessionDeleted} Discovery={DiscoveryDeleted}.",
            result.OutboxDeleted,
            result.ProcessedDeleted,
            result.AuditDeleted,
            result.MonitoringDeleted,
            result.NotificationDeleted,
            result.RemoteSessionDeleted,
            result.DiscoveryDeleted);
    }
}

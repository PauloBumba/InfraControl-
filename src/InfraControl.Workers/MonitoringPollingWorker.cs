using InfraControl.Application.Monitoring.Polling;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InfraControl.Workers;

// F5-T007 (MON-010): polling periódico de monitoring — worker fino no
// padrão CleanupWorker: lê config, agenda o serviço (regra fica no
// Application, testável sem host). Enabled=true e 60s por default.

public sealed class MonitoringPollingWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MonitoringPollingWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    private readonly bool _enabled = configuration.GetValue("MonitoringPolling:Enabled", true);
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(
        Math.Max(1, configuration.GetValue("MonitoringPolling:IntervalSeconds", 60)));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            logger.LogInformation("Monitoring polling worker is disabled via MonitoringPolling:Enabled=false.");
            return;
        }

        logger.LogInformation(
            "Monitoring polling worker started with interval {Interval}.", _interval);

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
                logger.LogError(exception, "Monitoring polling run failed.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var polling = scope.ServiceProvider.GetRequiredService<IMonitoringPollingService>();

        var executed = await polling.PollOnceAsync(cancellationToken);

        logger.LogInformation(
            "Monitoring polling completed: {Executed} checks executed.", executed);
    }
}

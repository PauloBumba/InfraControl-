using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfraControl.Workers.Backup;

/// <summary>
/// Backup periódico do PostgreSQL (F12-T003): executa
/// <see cref="PostgresBackupService"/> a cada <c>Backup:IntervalHours"/>
/// (primeira execução imediata, como o CleanupWorker).
/// </summary>
public sealed class BackupWorker(
    PostgresBackupService backupService,
    IOptions<BackupOptions> options,
    ILogger<BackupWorker> logger) : BackgroundService
{
    private readonly BackupOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Backup worker is disabled via Backup:Enabled=false.");
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, _options.IntervalHours));
        logger.LogInformation("Backup worker started with interval {Interval}.", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await backupService.RunOnceAsync(stoppingToken);

                if (!result.Success)
                    logger.LogError("Backup run failed: {Error}", result.Error);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Backup worker run failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

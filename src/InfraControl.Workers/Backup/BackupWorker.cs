using System.Diagnostics;
using InfraControl.Infrastructure.Caching;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfraControl.Workers.Backup;

/// <summary>
/// Backup periódico do PostgreSQL (F12-T003): executa
/// <see cref="PostgresBackupService"/> a cada <c>Backup:IntervalHours"/>
/// (primeira execução imediata, como o CleanupWorker).
/// ADR-044: Usa lock distribuído para prevenir duplicação em múltiplas instâncias.
/// </summary>
public sealed class BackupWorker(
    PostgresBackupService backupService,
    IOptions<BackupOptions> options,
    ILogger<BackupWorker> logger,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    private readonly BackupOptions _options = options.Value;

    /// <summary>
    /// Settings efetivas: override do banco (<c>backup_settings</c>, editável
    /// pela plataforma — spec evolução §9) sobre <c>Backup:*</c> do
    /// appsettings. Re-lido a cada ciclo para o admin não precisar reiniciar.
    /// </summary>
    private static async Task<BackupOptions> EffectiveOptionsAsync(
        IServiceScope scope, BackupOptions fallback, CancellationToken ct)
    {
        try
        {
            var repository = scope.ServiceProvider
                .GetService<Application.Operations.Backup.Ports.IBackupSettingsRepository>();
            var settings = repository is null
                ? null
                : await repository.GetAsync(ct);
            if (settings is null)
                return fallback;

            return new BackupOptions
            {
                Enabled = settings.Enabled,
                IntervalHours = settings.IntervalHours,
                RetentionDays = settings.RetentionDays,
                Path = settings.Path,
                FileNamePrefix = settings.FileNamePrefix,
                TimeoutSeconds = settings.TimeoutSeconds,
                ConnectionString = fallback.ConnectionString,
                AdditionalTargets = fallback.AdditionalTargets,
            };
        }
        catch (Exception)
        {
            // Banco indisponível → mantém os defaults do appsettings.
            return fallback;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        BackupOptions initial;
        using (var bootScope = scopeFactory.CreateScope())
            initial = await EffectiveOptionsAsync(bootScope, _options, stoppingToken);
        if (!initial.Enabled)
        {
            logger.LogInformation("Backup worker is disabled via Backup:Enabled=false.");
            return;
        }

        logger.LogInformation(
            "Backup worker started with interval {Interval}h.", initial.IntervalHours);

        var interval = TimeSpan.FromHours(Math.Max(1, initial.IntervalHours));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var activity = WorkerTracing.Source.StartActivity(
                    "worker backup", ActivityKind.Internal);
                using var scope = scopeFactory.CreateScope();
                var effective = await EffectiveOptionsAsync(scope, _options, stoppingToken);
                // Intervalo efetivo atualizado a cada ciclo (override do banco).
                interval = TimeSpan.FromHours(Math.Max(1, effective.IntervalHours));
                if (!effective.Enabled)
                {
                    logger.LogDebug("Backup desabilitado via configuração da plataforma.");
                }
                else
                {
                    var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

                    // ADR-044: Lock distribuído para prevenir execução duplicada
                    var lockKey = "worker:backup";
                    var lockExpiry = TimeSpan.FromHours(2); // Lock expira após 2h (backup pode demorar)

                    using var @lock = await lockService.TryAcquireLockAsync(lockKey, lockExpiry, stoppingToken);
                    if (@lock == null)
                    {
                        logger.LogDebug("Backup worker skipped: lock já adquirido por outra instância.");
                    }
                    else
                    {
                        logger.LogInformation("Backup worker: lock adquirido, iniciando execução.");
                        var result = await backupService.RunOnceAsync(effective, stoppingToken);

                        if (!result.Success)
                            logger.LogError("Backup run failed: {Error}", result.Error);
                    }
                }
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

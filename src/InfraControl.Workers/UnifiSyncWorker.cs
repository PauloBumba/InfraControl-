using InfraControl.Application.Integrations.Unifi;
using MediatR;

namespace InfraControl.Workers;

/// <summary>
/// F26 Fase 10.2: sincronização automática UniFi pelo job existente.
/// Intervalo configurável com limites seguros (5min–24h); trava
/// atômica impede execuções simultâneas; timeout por execução; falha
/// transitória NUNCA apaga dados (o sync só upserta — Fase 3). O
/// handler é chamado DIRETAMENTE (sem pipeline MediatR): a
/// AuthorizationBehavior exige usuário HTTP autenticado por decisão
/// explícita (workers não têm sessão); rastreabilidade fica por conta
/// do `UniFiSyncRun` gravado pelo handler (Fase 9).
/// </summary>
public sealed class UnifiSyncWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<UnifiSyncWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxInterval = TimeSpan.FromHours(24);
    public static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(2);

    private readonly bool _enabled = configuration.GetValue("UnifiSync:Enabled", true);
    private readonly TimeSpan _interval = ClampInterval(
        TimeSpan.FromMinutes(configuration.GetValue("UnifiSync:IntervalMinutes", 30)));

    private int _running;

    /// <summary>Limites seguros do intervalo (10.2).</summary>
    public static TimeSpan ClampInterval(TimeSpan interval)
    {
        if (interval < MinInterval) return MinInterval;
        return interval > MaxInterval ? MaxInterval : interval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            logger.LogInformation("Unifi sync worker is disabled via UnifiSync:Enabled=false.");
            return;
        }

        logger.LogInformation("Unifi sync worker started with interval {Interval}.", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            {
                logger.LogWarning(
                    "Unifi sync skipped: previous run still in progress.");
                continue;
            }

            try
            {
                await RunOnceAsync(stoppingToken);
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(RunTimeout);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<
                IRequestHandler<SyncUnifiInventoryCommand, UnifiSyncResultDto>>();

            var result = await handler.Handle(
                new SyncUnifiInventoryCommand(), timeout.Token);

            logger.LogInformation(
                "Unifi sync finished: {Created} created, {Updated} updated, " +
                "{References} references, {Errors} errors, {Sites} sites.",
                result.Created, result.Updated, result.References, result.Errors, result.Sites);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            logger.LogWarning("Unifi sync cancelled by timeout after {Timeout}.", RunTimeout);
        }
        catch (OperationCanceledException)
        {
            // shutdown — não loga como erro
        }
        catch (Exception exception)
        {
            // falha transitória: dados anteriores permanecem (sync só upserta)
            logger.LogError(exception, "Unifi sync run failed.");
        }
    }
}

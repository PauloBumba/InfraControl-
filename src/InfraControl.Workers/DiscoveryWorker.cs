using System.Diagnostics;
using InfraControl.Application.Discovery.Runs;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Workers;

/// <summary>
/// Network Explorer F4 — sincronização periódica de discovery (spec §7).
/// Varre os devices de infraestrutura de rede (Switch/Router/Firewall/
/// AccessPoint) com IP cadastrado, executando a varredura completa de
/// providers por device (RunDiscoverySweepCommand). Intervalo
/// configurável com limites seguros (15min–24h); trava atômica impede
/// execuções simultâneas; timeout por ciclo. O handler é chamado
/// DIRETAMENTE (sem pipeline MediatR) — mesmo padrão do UnifiSyncWorker:
/// workers não têm sessão HTTP e a rastreabilidade fica nos
/// DiscoveryRun gravados por provider.
/// </summary>
public sealed class DiscoveryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<DiscoveryWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MaxInterval = TimeSpan.FromHours(24);
    public static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(30);

    private static readonly DeviceType[] SweepTypes =
        [DeviceType.Switch, DeviceType.Router, DeviceType.Firewall, DeviceType.AccessPoint];

    private readonly bool _enabled = configuration.GetValue("Discovery:Enabled", true);
    private readonly TimeSpan _interval = ClampInterval(
        TimeSpan.FromMinutes(configuration.GetValue("Discovery:IntervalMinutes", 60)));

    private int _running;

    public static TimeSpan ClampInterval(TimeSpan interval)
    {
        if (interval < MinInterval) return MinInterval;
        return interval > MaxInterval ? MaxInterval : interval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            logger.LogInformation("Discovery worker is disabled via Discovery:Enabled=false.");
            return;
        }

        logger.LogInformation("Discovery worker started with interval {Interval}.", _interval);

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
                logger.LogWarning("Discovery sweep skipped: previous run still in progress.");
                continue;
            }

            try
            {
                using var activity = WorkerTracing.Source.StartActivity(
                    "worker discovery-sweep", ActivityKind.Internal);
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
            var devices = scope.ServiceProvider
                .GetRequiredService<IDeviceRepository>();
            var sweepHandler = scope.ServiceProvider.GetRequiredService<
                IRequestHandler<RunDiscoverySweepCommand, IReadOnlyList<DiscoveryRunDto>>>();

            var targets = (await devices.GetAllOrderedByNameAsync(timeout.Token))
                .Where(d => SweepTypes.Contains(d.Type)
                    && !string.IsNullOrWhiteSpace(d.Ip))
                .ToArray();

            var completed = 0;
            foreach (var device in targets)
            {
                timeout.Token.ThrowIfCancellationRequested();
                try
                {
                    var runs = await sweepHandler.Handle(
                        new RunDiscoverySweepCommand(device.Id), timeout.Token);
                    completed += runs.Count;
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // Falha de um device não aborta a varredura dos demais.
                    logger.LogError(
                        exception,
                        "Discovery sweep failed for device {DeviceId} ({Name}).",
                        device.Id, device.Name);
                }
            }

            logger.LogInformation(
                "Discovery sweep finished: {Devices} devices, {Runs} runs.",
                targets.Length, completed);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            logger.LogWarning("Discovery sweep cancelled by timeout after {Timeout}.", RunTimeout);
        }
        catch (OperationCanceledException)
        {
            // shutdown — não loga como erro
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Discovery sweep run failed.");
        }
    }
}

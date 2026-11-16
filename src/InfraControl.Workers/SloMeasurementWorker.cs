using System.Diagnostics;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.Application.Sla.Slos.Bindings;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfraControl.Workers;

/// <summary>
/// Medição automática de SLO (ADR-068): a cada
/// <c>SloMeasurement:IntervalMinutes</c> (default 60, clamp 15–1440)
/// calcula a disponibilidade real — % de <see cref="MonitoringResult"/>
/// com sucesso dos checks vinculados (<see cref="SloCheckBinding"/>) na
/// janela do SLO — e grava uma <see cref="SloMeasurement"/> imutável.
///
/// Regras honestas: SLO sem vínculo não é medido (medição manual segue
/// valendo); janela sem nenhum poll (total = 0) não gera medição —
/// a UI mostra "Sem medição", nunca um 100% inventado; check
/// desabilitada não entra no cálculo (não gera polls novos).
/// </summary>
public sealed class SloMeasurementWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SloMeasurementWorker> logger) : BackgroundService
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MaxInterval = TimeSpan.FromHours(24);
    public static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(10);

    private readonly bool _enabled = configuration.GetValue("SloMeasurement:Enabled", true);
    private readonly TimeSpan _interval = ClampInterval(
        TimeSpan.FromMinutes(configuration.GetValue("SloMeasurement:IntervalMinutes", 60)));

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
            logger.LogInformation("SLO measurement worker is disabled via SloMeasurement:Enabled=false.");
            return;
        }

        logger.LogInformation("SLO measurement worker started with interval {Interval}.", _interval);

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
                continue;

            try
            {
                using var activity = WorkerTracing.Source.StartActivity(
                    "worker slo-measurement", ActivityKind.Internal);
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
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;

            var bindings = await db.SloCheckBindings.AsNoTracking()
                .ToListAsync(timeout.Token);
            if (bindings.Count == 0)
                return;

            var sloIds = bindings.Select(b => b.SloId).Distinct().ToArray();
            var slos = await db.Slos.AsNoTracking()
                .Where(slo => sloIds.Contains(slo.Id))
                .ToListAsync(timeout.Token);
            if (slos.Count == 0)
                return;

            var checkIds = bindings.Select(b => b.MonitoringCheckId).Distinct().ToArray();
            var checks = await db.MonitoringChecks.AsNoTracking()
                .Where(check => checkIds.Contains(check.Id))
                .ToDictionaryAsync(check => check.Id, timeout.Token);

            var bindingsBySlo = bindings
                .GroupBy(b => b.SloId)
                .ToDictionary(g => g.Key, g => g.Select(b => b.MonitoringCheckId).ToArray());

            // Uma agregação por janela distinta — exata e barata (janelas
            // reais se repetem; SLOs sem binding nem chegam aqui).
            var resultsRepository = scope.ServiceProvider
                .GetRequiredService<IMonitoringResultRepository>();
            var countsByWindow = new Dictionary<int, IReadOnlyList<DeviceKindPollCount>>();
            foreach (var windowDays in slos.Select(slo => slo.WindowDays).Distinct())
            {
                countsByWindow[windowDays] = await resultsRepository.GetSuccessCountsByDeviceKindAsync(
                    now.AddDays(-windowDays), timeout.Token);
            }

            // Idempotência: se o ciclo anterior já gravou medição dentro do
            // intervalo corrente (restart no meio do período, sobreposição
            // de deploy), não duplica — uma observação por janela de tempo.
            var recentlyMeasured = (await db.SloMeasurements.AsNoTracking()
                    .Where(measurement => sloIds.Contains(measurement.SloId)
                        && measurement.MeasuredAt >= now - _interval)
                    .Select(measurement => measurement.SloId)
                    .Distinct()
                    .ToListAsync(timeout.Token))
                .ToHashSet();

            var measurements = new List<SloMeasurement>();
            var skippedNoData = 0;
            var skippedDuplicate = 0;

            foreach (var slo in slos)
            {
                if (!bindingsBySlo.TryGetValue(slo.Id, out var boundIds))
                    continue;
                if (recentlyMeasured.Contains(slo.Id))
                {
                    skippedDuplicate++;
                    continue;
                }

                // Pares (DeviceId, Kind) das checks habilitadas vinculadas.
                var pairs = boundIds
                    .Select(id => checks.GetValueOrDefault(id))
                    .Where(check => check is { Enabled: true })
                    .Select(check => (check!.DeviceId, check.Kind))
                    .ToList();

                var counts = countsByWindow[slo.WindowDays];
                var value = SloAvailabilityCalculator.ComputePercent(pairs, counts);
                if (value is null)
                {
                    skippedNoData++;
                    continue;
                }

                measurements.Add(SloMeasurement.Create(slo.Id, now, value.Value));
            }

            if (measurements.Count == 0)
            {
                if (skippedNoData > 0 || skippedDuplicate > 0)
                    logger.LogInformation(
                        "SLO measurement: {Skipped} SLO(s) sem polls na janela, {Dup} já medidos neste ciclo — nenhuma medição nova.",
                        skippedNoData, skippedDuplicate);
                return;
            }

            db.SloMeasurements.AddRange(measurements);
            await db.SaveChangesAsync(timeout.Token);

            logger.LogInformation(
                "SLO measurement: {Count} medição(ões) gravada(s) em {Slos} SLO(s); {Skipped} sem dados, {Dup} já medidos.",
                measurements.Count, slos.Count, skippedNoData, skippedDuplicate);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            logger.LogWarning("SLO measurement cycle cancelled by timeout after {Timeout}.", RunTimeout);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "SLO measurement cycle failed.");
        }
    }
}

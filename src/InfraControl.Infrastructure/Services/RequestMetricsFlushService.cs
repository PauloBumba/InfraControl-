using System.Diagnostics;
using InfraControl.Application.Operations.Cluster.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Drena o <see cref="RequestMetricsRecorder"/> a cada 30s e faz merge no
/// Postgres via <see cref="IRequestMetricRepository"/>. Falha de banco não
/// derruba o serviço — o delta é descartado (métricas são best-effort).
/// </summary>
public sealed class RequestMetricsFlushService(
    RequestMetricsRecorder recorder,
    IServiceScopeFactory scopeFactory,
    ILogger<RequestMetricsFlushService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await FlushOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await FlushOnceAsync(CancellationToken.None);
        await base.StopAsync(cancellationToken);
    }

    private static readonly ActivitySource ActivitySource = new("InfraControl.Infrastructure");

    private async Task FlushOnceAsync(CancellationToken cancellationToken)
    {
        var deltas = recorder.Drain();
        if (deltas.Count == 0) return;

        // Span por flush — o upsert no Postgres aparece como filho no Tempo.
        using var activity = ActivitySource.StartActivity(
            "worker request-metrics-flush", ActivityKind.Internal);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRequestMetricRepository>();
            await repo.UpsertDeltasAsync(deltas, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao persistir métricas de requisição ({Count} deltas descartados).", deltas.Count);
        }
    }
}

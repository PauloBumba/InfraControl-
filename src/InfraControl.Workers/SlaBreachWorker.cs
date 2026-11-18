using System.Diagnostics;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfraControl.Workers;

/// <summary>
/// Vigia de SLA de tasks: a cada minuto marca como violados os prazos
/// ResponseDueAt/ResolutionDueAt vencidos em tasks ainda abertas e deixa
/// o SaveChanges disparar o TaskSlaBreachedDomainEvent (AppDbContext →
/// mediator → outbox → NotificationEventConsumer). Idempotente por
/// construção: ResponseBreachedAt/ResolutionBreachedAt marcam uma única
/// vez — rerodar o ciclo não duplica notificação.
/// </summary>
public sealed class SlaBreachWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SlaBreachWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(2);

    private static readonly TaskState[] OpenStates =
    [
        TaskState.Open,
        TaskState.InProgress,
        TaskState.Waiting,
        TaskState.Blocked,
    ];

    private int _running;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SLA breach worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
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
                    "worker sla-breach", ActivityKind.Internal);
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

            var slas = await db.TaskSlas
                .Where(sla => db.Tasks.Any(task =>
                    task.Id == sla.TaskId && OpenStates.Contains(task.State)))
                .Where(sla =>
                    (sla.ResponseDueAt <= now && sla.ResponseBreachedAt == null) ||
                    (sla.ResolutionDueAt <= now && sla.ResolutionBreachedAt == null))
                .ToListAsync(timeout.Token);

            if (slas.Count == 0)
                return;

            var taskIds = slas.Select(sla => sla.TaskId).ToArray();
            var titles = await db.Tasks.AsNoTracking()
                .Where(task => taskIds.Contains(task.Id))
                .ToDictionaryAsync(task => task.Id, task => task.Title, timeout.Token);

            var marked = 0;
            foreach (var sla in slas)
            {
                var title = titles.GetValueOrDefault(sla.TaskId, "task");
                if (sla.MarkBreached(SlaBreachKind.Response, now, title))
                    marked++;
                if (sla.MarkBreached(SlaBreachKind.Resolution, now, title))
                    marked++;
            }

            if (marked == 0)
                return;

            await db.SaveChangesAsync(timeout.Token);

            logger.LogInformation(
                "SLA breach: {Count} prazo(s) marcado(s) como violado(s) em {Slas} task(s).",
                marked, slas.Count);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            logger.LogWarning("SLA breach scan cancelled by timeout after {Timeout}.", RunTimeout);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "SLA breach cycle failed.");
        }
    }
}

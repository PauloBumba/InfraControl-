using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Common;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InfraControl.Api.HealthChecks;

/// <summary>
/// F46-T006 — saúde dos workers indiretamente: o backlog do outbox é o
/// termômetro do <c>OutboxDispatcherWorker</c> (processo separado, sem porta
/// para checar). Mensagens não processadas há mais de 5 minutos = workers
/// atrasados; a partir de 100 = Unhealthy.
/// </summary>
public sealed class OutboxBacklogHealthCheck(AppDbContext db) : IHealthCheck
{
    public const int DegradedAfterMinutes = 5;
    public const int UnhealthyThreshold = 100;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var cutoff = AppTimeProvider.Current.GetUtcNow().UtcDateTime
            .AddMinutes(-DegradedAfterMinutes);
        var stale = await db.OutboxMessages.CountAsync(
            message => message.ProcessedAt == null && message.OccurredAt < cutoff,
            cancellationToken);

        var data = new Dictionary<string, object>
        {
            ["staleMessages"] = stale,
            ["thresholdMinutes"] = DegradedAfterMinutes
        };

        return stale switch
        {
            0 => HealthCheckResult.Healthy("Outbox sem backlog atrasado.", data: data),
            >= UnhealthyThreshold => HealthCheckResult.Unhealthy(
                $"{stale} mensagens do outbox atrasadas há mais de " +
                $"{DegradedAfterMinutes} min — workers parados?",
                data: data),
            _ => HealthCheckResult.Degraded(
                $"{stale} mensagens do outbox atrasadas há mais de " +
                $"{DegradedAfterMinutes} min.",
                data: data)
        };
    }
}

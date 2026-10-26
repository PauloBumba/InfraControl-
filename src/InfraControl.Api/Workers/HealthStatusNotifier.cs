using InfraControl.Api.Hubs;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InfraControl.Api.Workers;

/// <summary>
/// F46-T006 — aviso de mudança de saúde via SignalR: avalia o agregado de
/// health checks a cada 60s e, quando o status GLOBAL muda (ex.: Healthy →
/// Degraded), publica <c>health-status</c> para os clientes conectados — a
/// HealthPage recarrega e mostra o banner sem o usuário precisar refrescar.
/// </summary>
public sealed class HealthStatusNotifier(
    HealthCheckService healthCheckService,
    IHubContext<NotificationsHub> hubContext,
    ILogger<HealthStatusNotifier> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Notificador de saúde iniciado (intervalo {Interval}).", Interval);

        HealthStatus? previous = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var report = await healthCheckService.CheckHealthAsync(stoppingToken);

                if (previous is null)
                {
                    // Primeira amostra só fixa a linha de base (não é "mudança").
                    previous = report.Status;
                }
                else if (report.Status != previous)
                {
                    var before = previous.Value;
                    var current = report.Status;
                    previous = current;

                    var unhealthy = report.Entries
                        .Where(entry => entry.Value.Status != HealthStatus.Healthy)
                        .Select(entry => entry.Key)
                        .ToArray();

                    await hubContext.Clients.All.SendAsync("health-status", new
                    {
                        status = current.ToString(),
                        previous = before.ToString(),
                        changedAt = DateTimeOffset.UtcNow,
                        unhealthy
                    }, stoppingToken);

                    logger.LogWarning(
                        "Saúde da plataforma mudou para {Status} (problemas: {Unhealthy}).",
                        current, string.Join(", ", unhealthy));
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha ao avaliar a saúde da plataforma.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

using InfraControl.Application.AI.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InfraControl.Infrastructure.AI;

/// <summary>
/// <c>IHealthCheck</c> da camada de IA (FAI-T006/S5 · docs/34 §44):
/// adapta o <see cref="IAiRuntimeHealth"/> ao health endpoint do host.
///
/// Estados "não configurado" (capacidade sem provider, Ollama fora do
/// ar) viram <c>Degraded</c> — HTTP 200 — e nunca <c>Unhealthy</c>:
/// o sistema deve subir e operar sem IA (ADR-036 · docs/34 §44/§46).
/// O detalhe por componente vai no payload do health check.
/// </summary>
public sealed class AiRuntimeHealthCheck(IAiRuntimeHealth runtimeHealth) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var report = await runtimeHealth.CheckAsync(cancellationToken);

        var data = report.Components.ToDictionary(
            component => component.Component,
            component => (object)(component.Detail is null
                ? component.Status.ToString()
                : $"{component.Status} — {component.Detail}"));

        return report.IsHealthy
            ? HealthCheckResult.Healthy("Camada de IA pronta (capacidades mapeadas, providers alcançáveis).", data)
            : HealthCheckResult.Degraded(
                "Camada de IA degradada (estado 'não configurado' §44 — o sistema segue sem IA).",
                data: data);
    }
}

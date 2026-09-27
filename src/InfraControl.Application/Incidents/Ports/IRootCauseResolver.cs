using InfraControl.Domain.Entities;

namespace InfraControl.Application.Incidents.Ports;

/// <summary>
/// F6-T005 — Root Cause: causa provável pt-BR do incidente (heurística
/// fixa em código, como os thresholds de alert).
/// </summary>
public interface IRootCauseResolver
{
    /// <summary>
    /// Heurística: (1) upstream alcançável com Incident Open → falha no
    /// upstream; (2) vizinho com alerta de Ping Open mais antigo que o
    /// incidente → enlace/switch do vizinho; (3) senão → falha local.
    /// </summary>
    Task<string?> ResolveAsync(
        Incident incident, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalcula a causa do Incident Open do device (avaliador chama a
    /// cada ciclo — cobre a corrida em que o upstream abre depois).
    /// </summary>
    Task RefreshAsync(
        Guid deviceId, CancellationToken cancellationToken = default);
}

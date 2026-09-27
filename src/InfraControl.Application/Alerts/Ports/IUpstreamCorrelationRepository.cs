namespace InfraControl.Application.Alerts.Ports;

/// <summary>
/// F6-T004 — Correlation (supressão topológica): raiz do problema via
/// conexões de topologia (F3-T005). Não é repositório de entidade
/// (sem IRepository) — só consulta; o RepositoryRegistrar aceita por
/// convenção de nome.
/// </summary>
public interface IUpstreamCorrelationRepository
{
    /// <summary>
    /// BFS não-dirigido pelas Connections a partir dos vizinhos de
    /// <paramref name="deviceId"/>: primeiro device alcançável (si-mesmo
    /// excluído) com Incident Open e o incidente. null = sem supressor.
    /// </summary>
    Task<UpstreamIncident?> FindOpenIncidentUpstreamAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);
}

/// <summary>Incidente Open de um upstream alcançável por conexão.</summary>
public sealed record UpstreamIncident(Guid DeviceId, Guid IncidentId);

using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Monitoring.Events.Ports;

/// <summary>
/// ADR-068: estados de problema lógico por chave de correlação — a máquina
/// de estados da ingestão (PROBLEM repetido aberto suprime Alert/Incident;
/// OK resolve via AlertId do ciclo).
/// </summary>
public interface IMonitoringProblemStateRepository : IRepository<MonitoringProblemState>
{
    /// <summary>Estado pelo par (Source, CorrelationKey) — com tracking.</summary>
    Task<MonitoringProblemState?> GetBySourceAndCorrelationKeyAsync(
        MonitoringSource source,
        string correlationKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estado pelo Alert do ciclo atual — caminho do OK (todo problem
    /// externo gera exatamente um Alert; a recuperação já o localizou por
    /// (Source, ExternalProblemId)).
    /// </summary>
    Task<MonitoringProblemState?> GetByAlertIdAsync(
        Guid alertId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ADR-069: estado mais recente do device (qualquer status) — usado
    /// pelo cartão WhatsApp para recuperar triggerId (CorrelationKey) e a
    /// janela OpenedAt→ResolvedAt do ciclo. Aproximação correta porque a
    /// entrega é quase em tempo real.
    /// </summary>
    Task<MonitoringProblemState?> GetLatestByDeviceAsync(
        MonitoringSource source,
        Guid deviceId,
        CancellationToken cancellationToken = default);
}

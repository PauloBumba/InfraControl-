using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Sla.Slos.Bindings.Ports;

/// <summary>
/// Vínculos Slo ↔ MonitoringCheck (ADR-068). A chave é o par
/// (SloId, MonitoringCheckId) — mesmo molde de IncidentAlert.
/// </summary>
public interface ISloCheckBindingRepository : IRepository<SloCheckBinding>
{
    /// <summary>Ids de checks vinculados por SLO (bulk p/ enriquecer a listagem).</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetCheckIdsBySloIdsAsync(
        IReadOnlyCollection<Guid> sloIds,
        CancellationToken cancellationToken = default);

    /// <summary>Todos os vínculos (worker de medição — base pequena por natureza).</summary>
    Task<IReadOnlyList<SloCheckBinding>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Vínculos de um SLO (edição do formulário).</summary>
    Task<IReadOnlyList<SloCheckBinding>> GetBySloIdAsync(
        Guid sloId, CancellationToken cancellationToken = default);

    /// <summary>Check está vinculado a algum SLO? (guard de exclusão → 409).</summary>
    Task<bool> HasBindingsForCheckAsync(
        Guid monitoringCheckId, CancellationToken cancellationToken = default);

    /// <summary>Persiste os vínculos (diff já resolvido pelo handler).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

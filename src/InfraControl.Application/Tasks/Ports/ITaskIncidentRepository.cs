using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Tasks.Ports;

/// <summary>F7-T004: incidentes vinculados à Task (N:N com Incident).</summary>
public interface ITaskIncidentRepository : IRepository<TaskIncident>
{
    Task<bool> ExistsAsync(
        Guid taskId, Guid incidentId, CancellationToken cancellationToken = default);

    Task<TaskIncident?> FindAsync(
        Guid taskId, Guid incidentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> GetIncidentIdsAsync(
        Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>Group join por página — evita N+1 no Search (F7-T004).</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetIncidentIdsByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

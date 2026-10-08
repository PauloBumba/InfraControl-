using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Tasks.Ports;

/// <summary>Impedimentos estruturados da Task (F23-T004/P4b — §10).</summary>
public interface ITaskImpedimentRepository : IRepository<TaskImpediment>
{
    Task<IReadOnlyCollection<TaskImpediment>> GetByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

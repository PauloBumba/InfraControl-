using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Tasks.Ports;

/// <summary>Critérios de aceite da Task (F23-T003 — §12).</summary>
public interface ITaskCriterionRepository : IRepository<TaskCriterion>
{
    Task<IReadOnlyCollection<TaskCriterion>> GetByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

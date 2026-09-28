using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Sla.TaskSlas.Ports;

public interface ITaskSlaPauseRepository : IRepository<TaskSlaPause>
{
    Task<TaskSlaPause?> GetOpenByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskSlaPause>> GetByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

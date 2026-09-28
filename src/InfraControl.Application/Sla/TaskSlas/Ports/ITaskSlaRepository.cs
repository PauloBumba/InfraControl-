using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Sla.TaskSlas.Ports;

public interface ITaskSlaRepository : IRepository<TaskSla>
{
    /// <summary>PK = TaskId (1:1) — mesma coisa que GetByIdAsync.</summary>
    Task<TaskSla?> GetByTaskIdAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>Usado pelo DeleteSlaPolicy para bloquear exclusão com tasks aplicadas (F8-T003 → 409).</summary>
    Task<bool> HasPolicySlasAsync(Guid slaPolicyId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

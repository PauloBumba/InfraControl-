using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Tasks.History.Ports;

/// <summary>Entrada do histórico com o nome do ator (F7-T006).</summary>
public sealed record TaskHistoryWithActor(TaskHistory Entry, string? ActorName);

public interface ITaskHistoryRepository : IRepository<TaskHistory>
{
    /// <summary>Entradas da task em ordem cronológica (com ator).</summary>
    Task<IReadOnlyCollection<TaskHistoryWithActor>> GetByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

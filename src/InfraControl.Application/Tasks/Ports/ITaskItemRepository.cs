using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Tasks.Ports;

public interface ITaskItemRepository : IRepository<TaskItem>
{
    /// <summary>
    /// Paginado, mais recente primeiro — filtros termo (título/descrição),
    /// tipo e estado (null = todos) — F7-T001.
    /// </summary>
    Task<PagedResult<TaskItem>> SearchAsync(
        string? term,
        TaskKind? kind,
        TaskState? state,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

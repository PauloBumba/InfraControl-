using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Tasks.Ports;

/// <summary>
/// F23-T004/P4a — estatísticas das subtarefas de um pai (1 consulta,
/// sem N+1): total geral, quantas não canceladas e quantas concluídas
/// (Resolved/Closed) — o `Progress` do `TaskDto` sai daqui.
/// </summary>
public sealed record TaskSubtaskStats(int Total, int NonCancelled, int Done);

public interface ITaskItemRepository : IRepository<TaskItem>
{
    /// <summary>
    /// Paginado, mais recente primeiro — filtros termo (título/descrição),
    /// tipo, estado (null = todos), pai (`parentId`: filhos de X) e
    /// `rootOnly` (só tasks sem pai) — F7-T001 + F23-T004/P4a.
    /// </summary>
    Task<PagedResult<TaskItem>> SearchAsync(
        string? term,
        TaskKind? kind,
        TaskState? state,
        int page,
        int pageSize,
        Guid? parentId = null,
        bool rootOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>Stats de subtarefas dos pais informados (1 consulta).</summary>
    Task<IReadOnlyDictionary<Guid, TaskSubtaskStats>> GetSubtaskStatsAsync(
        IReadOnlyCollection<Guid> parentIds,
        CancellationToken cancellationToken = default);

    /// <summary>Títulos das tasks informadas (pai das subtarefas).</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetTitlesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

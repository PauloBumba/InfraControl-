using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Tasks.Ports;

/// <summary>F23-T005/P5a: Knowledge vinculada à Task (N:N com artigo).</summary>
public interface ITaskKnowledgeArticleRepository : IRepository<TaskKnowledgeArticle>
{
    Task<bool> ExistsAsync(
        Guid taskId, Guid articleId, CancellationToken cancellationToken = default);

    Task<TaskKnowledgeArticle?> FindAsync(
        Guid taskId, Guid articleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> GetArticleIdsAsync(
        Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>Group join por página — evita N+1 no Search (P5a).</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetArticleIdsByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

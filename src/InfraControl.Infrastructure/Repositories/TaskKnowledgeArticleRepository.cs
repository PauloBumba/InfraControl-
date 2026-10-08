using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F23-T005/P5a: artigos da task com group join por página.</summary>
public sealed class TaskKnowledgeArticleRepository(AppDbContext dbContext)
    : ITaskKnowledgeArticleRepository
{
    public Task<TaskKnowledgeArticle?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        // O Id do par (TaskId, ArticleId) é unmapped (padrão TaskIncident).
        => throw new NotSupportedException(
            "TaskKnowledgeArticle usa FindAsync(taskId, articleId) — a chave é o par composto.");

    public async Task AddAsync(
        TaskKnowledgeArticle entity, CancellationToken cancellationToken = default)
        => await dbContext.TaskKnowledgeArticles.AddAsync(entity, cancellationToken);

    public void Remove(TaskKnowledgeArticle entity)
        => dbContext.TaskKnowledgeArticles.Remove(entity);

    public Task<bool> ExistsAsync(
        Guid taskId, Guid articleId, CancellationToken cancellationToken = default)
        => dbContext.TaskKnowledgeArticles.AnyAsync(
            link => link.TaskId == taskId && link.ArticleId == articleId,
            cancellationToken);

    public Task<TaskKnowledgeArticle?> FindAsync(
        Guid taskId, Guid articleId, CancellationToken cancellationToken = default)
        => dbContext.TaskKnowledgeArticles.FirstOrDefaultAsync(
            link => link.TaskId == taskId && link.ArticleId == articleId,
            cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetArticleIdsAsync(
        Guid taskId, CancellationToken cancellationToken = default)
        => await dbContext.TaskKnowledgeArticles.AsNoTracking()
            .Where(link => link.TaskId == taskId)
            .Select(link => link.ArticleId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>>
        GetArticleIdsByTaskIdsAsync(
            IReadOnlyCollection<Guid> taskIds,
            CancellationToken cancellationToken = default)
    {
        var pairs = await dbContext.TaskKnowledgeArticles.AsNoTracking()
            .Where(link => taskIds.Contains(link.TaskId))
            .Select(link => new { link.TaskId, link.ArticleId })
            .ToListAsync(cancellationToken);

        return pairs
            .GroupBy(pair => pair.TaskId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<Guid>)group
                    .Select(pair => pair.ArticleId).ToArray());
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// F24-T004/K4 — origens polimórficas do artigo (docs/32 §9).
/// Ordenação por tipo/id (determinística — a origem não tem ordem de
/// "mais nova").
/// </summary>
public sealed class KnowledgeArticleOriginRepository(AppDbContext dbContext)
    : IKnowledgeArticleOriginRepository
{
    public Task<KnowledgeArticleOrigin?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.KnowledgeArticleOrigins
            .SingleOrDefaultAsync(origin => origin.Id == id, cancellationToken);

    public async Task AddAsync(
        KnowledgeArticleOrigin entity, CancellationToken cancellationToken = default)
        => await dbContext.KnowledgeArticleOrigins.AddAsync(entity, cancellationToken);

    public void Remove(KnowledgeArticleOrigin entity)
        => dbContext.KnowledgeArticleOrigins.Remove(entity);

    public async Task<IReadOnlyCollection<KnowledgeArticleOrigin>> ListByArticleAsync(
        Guid articleId, CancellationToken cancellationToken = default)
        => await dbContext.KnowledgeArticleOrigins.AsNoTracking()
            .Where(origin => origin.ArticleId == articleId)
            .OrderBy(origin => origin.OriginType)
            .ThenBy(origin => origin.OriginId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<KnowledgeArticleOrigin>> ListByArticleIdsAsync(
        IReadOnlyCollection<Guid> articleIds, CancellationToken cancellationToken = default)
        => await dbContext.KnowledgeArticleOrigins.AsNoTracking()
            .Where(origin => articleIds.Contains(origin.ArticleId))
            .OrderBy(origin => origin.OriginType)
            .ThenBy(origin => origin.OriginId)
            .ToListAsync(cancellationToken);

    public Task<KnowledgeArticleOrigin?> FindAsync(
        Guid articleId,
        KnowledgeOriginType originType,
        Guid originId,
        CancellationToken cancellationToken = default)
        => dbContext.KnowledgeArticleOrigins
            .SingleOrDefaultAsync(origin =>
                origin.ArticleId == articleId
                && origin.OriginType == originType
                && origin.OriginId == originId,
                cancellationToken);

    public async Task<bool> ExistsAsync(
        Guid articleId,
        KnowledgeOriginType originType,
        Guid originId,
        CancellationToken cancellationToken = default)
        => await dbContext.KnowledgeArticleOrigins.AnyAsync(origin =>
            origin.ArticleId == articleId
            && origin.OriginType == originType
            && origin.OriginId == originId,
            cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

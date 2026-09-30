using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F10-T004: serviços do artigo (chave composta).</summary>
public sealed class KnowledgeArticleServiceRepository(AppDbContext dbContext)
    : IKnowledgeArticleServiceRepository
{
    public Task<KnowledgeArticleService?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        // O Id do par (ArticleId, ServiceId) é unmapped (padrão TaskDevice).
        => throw new NotSupportedException(
            "KnowledgeArticleService usa FindAsync(articleId, serviceId) — a chave é o par composto.");

    public async Task AddAsync(
        KnowledgeArticleService entity, CancellationToken cancellationToken = default)
        => await dbContext.KnowledgeArticleServices.AddAsync(entity, cancellationToken);

    public void Remove(KnowledgeArticleService entity)
        => dbContext.KnowledgeArticleServices.Remove(entity);

    public Task<bool> ExistsAsync(
        Guid articleId, Guid serviceId, CancellationToken cancellationToken = default)
        => dbContext.KnowledgeArticleServices.AnyAsync(
            link => link.ArticleId == articleId && link.ServiceId == serviceId,
            cancellationToken);

    public Task<KnowledgeArticleService?> FindAsync(
        Guid articleId, Guid serviceId, CancellationToken cancellationToken = default)
        => dbContext.KnowledgeArticleServices.FirstOrDefaultAsync(
            link => link.ArticleId == articleId && link.ServiceId == serviceId,
            cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetServiceIdsAsync(
        Guid articleId, CancellationToken cancellationToken = default)
        => await dbContext.KnowledgeArticleServices.AsNoTracking()
            .Where(link => link.ArticleId == articleId)
            .Select(link => link.ServiceId)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

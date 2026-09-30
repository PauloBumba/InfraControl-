using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F10-T004: incidentes do artigo (chave composta).</summary>
public sealed class KnowledgeArticleIncidentRepository(AppDbContext dbContext)
    : IKnowledgeArticleIncidentRepository
{
    public Task<KnowledgeArticleIncident?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        // O Id do par (ArticleId, IncidentId) é unmapped (padrão TaskDevice).
        => throw new NotSupportedException(
            "KnowledgeArticleIncident usa FindAsync(articleId, incidentId) — a chave é o par composto.");

    public async Task AddAsync(
        KnowledgeArticleIncident entity, CancellationToken cancellationToken = default)
        => await dbContext.KnowledgeArticleIncidents.AddAsync(entity, cancellationToken);

    public void Remove(KnowledgeArticleIncident entity)
        => dbContext.KnowledgeArticleIncidents.Remove(entity);

    public Task<bool> ExistsAsync(
        Guid articleId, Guid incidentId, CancellationToken cancellationToken = default)
        => dbContext.KnowledgeArticleIncidents.AnyAsync(
            link => link.ArticleId == articleId && link.IncidentId == incidentId,
            cancellationToken);

    public Task<KnowledgeArticleIncident?> FindAsync(
        Guid articleId, Guid incidentId, CancellationToken cancellationToken = default)
        => dbContext.KnowledgeArticleIncidents.FirstOrDefaultAsync(
            link => link.ArticleId == articleId && link.IncidentId == incidentId,
            cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetIncidentIdsAsync(
        Guid articleId, CancellationToken cancellationToken = default)
        => await dbContext.KnowledgeArticleIncidents.AsNoTracking()
            .Where(link => link.ArticleId == articleId)
            .Select(link => link.IncidentId)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

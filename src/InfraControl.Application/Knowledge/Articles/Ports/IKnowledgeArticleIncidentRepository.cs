using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Knowledge.Articles.Ports;

/// <summary>F10-T004: incidentes vinculados ao artigo (N:N com Incident).</summary>
public interface IKnowledgeArticleIncidentRepository : IRepository<KnowledgeArticleIncident>
{
    Task<bool> ExistsAsync(
        Guid articleId, Guid incidentId, CancellationToken cancellationToken = default);

    Task<KnowledgeArticleIncident?> FindAsync(
        Guid articleId, Guid incidentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> GetIncidentIdsAsync(
        Guid articleId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

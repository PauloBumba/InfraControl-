using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Knowledge.Articles.Ports;

/// <summary>F10-T004: serviços vinculados ao artigo (N:N com Service).</summary>
public interface IKnowledgeArticleServiceRepository : IRepository<KnowledgeArticleService>
{
    Task<bool> ExistsAsync(
        Guid articleId, Guid serviceId, CancellationToken cancellationToken = default);

    Task<KnowledgeArticleService?> FindAsync(
        Guid articleId, Guid serviceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> GetServiceIdsAsync(
        Guid articleId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

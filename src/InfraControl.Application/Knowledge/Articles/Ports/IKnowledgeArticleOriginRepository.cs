using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Knowledge.Articles.Ports;

/// <summary>
/// Origens do artigo (F24-T004/K4, spec docs/32 §9) — múltiplas
/// origens por artigo (Task/PDI/Chamado), polimórficas (sem FK).
/// </summary>
public interface IKnowledgeArticleOriginRepository
    : IRepository<KnowledgeArticleOrigin>
{
    /// <summary>Origens de um artigo (detalhe).</summary>
    Task<IReadOnlyCollection<KnowledgeArticleOrigin>> ListByArticleAsync(
        Guid articleId, CancellationToken cancellationToken = default);

    /// <summary>Origens de várias páginas num round-trip (busca — sem N+1).</summary>
    Task<IReadOnlyCollection<KnowledgeArticleOrigin>> ListByArticleIdsAsync(
        IReadOnlyCollection<Guid> articleIds, CancellationToken cancellationToken = default);

    Task<KnowledgeArticleOrigin?> FindAsync(
        Guid articleId,
        KnowledgeOriginType originType,
        Guid originId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid articleId,
        KnowledgeOriginType originType,
        Guid originId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

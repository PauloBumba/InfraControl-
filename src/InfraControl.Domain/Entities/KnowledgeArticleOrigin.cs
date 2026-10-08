using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Origem N:N do artigo Knowledge (F24-T004/K4, spec docs/32 §9) —
/// "Knowledge #0015 ← Task #0042": de onde o artigo veio, com
/// múltiplas origens permitidas (Task/PDI/Chamado).
///
/// Diferente do vínculo de uso (F10-T004 / P5a — "esta task usa este
/// artigo"), a origem é artigo-centro e polimórfica: `OriginId` não
/// tem FK (o alvo é de outro agregado — precedente das refs sem FK
/// do ADR-033); a existência é validada no handler por tipo.
/// </summary>
public sealed class KnowledgeArticleOrigin : AggregateRoot
{
    public Guid ArticleId { get; private set; }
    public KnowledgeOriginType OriginType { get; private set; }
    public Guid OriginId { get; private set; }

    private KnowledgeArticleOrigin() { }

    /// <summary>ADR-030 §5 — desvinculação (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new KnowledgeArticleOriginDeletedDomainEvent(
            ArticleId, OriginType, OriginId));

    public static KnowledgeArticleOrigin Create(
        Guid articleId, KnowledgeOriginType originType, Guid originId)
    {
        if (articleId == Guid.Empty)
            throw new ArgumentException("O id do artigo é obrigatório.", nameof(articleId));
        if (originId == Guid.Empty)
            throw new ArgumentException("O id da origem é obrigatório.", nameof(originId));

        var origin = new KnowledgeArticleOrigin
        {
            ArticleId = articleId,
            OriginType = originType,
            OriginId = originId,
        };
        origin.RaiseDomainEvent(new KnowledgeArticleOriginCreatedDomainEvent(
            origin.ArticleId, origin.OriginType, origin.OriginId));
        return origin;
    }
}

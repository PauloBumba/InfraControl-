using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>Vínculo N:N KnowledgeArticle ↔ Service (F10-T004).</summary>
public sealed class KnowledgeArticleService : AggregateRoot
{
    public Guid ArticleId { get; private set; }
    public Guid ServiceId { get; private set; }

    private KnowledgeArticleService() { }

    /// <summary>ADR-030 §5 — desvinculação (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new KnowledgeArticleServiceDeletedDomainEvent(ArticleId, ServiceId));

    public static KnowledgeArticleService Create(Guid articleId, Guid serviceId)
    {
        if (articleId == Guid.Empty)
            throw new ArgumentException("O id do artigo é obrigatório.", nameof(articleId));
        if (serviceId == Guid.Empty)
            throw new ArgumentException("O id do serviço é obrigatório.", nameof(serviceId));

        var link = new KnowledgeArticleService { ArticleId = articleId, ServiceId = serviceId };
        link.RaiseDomainEvent(new KnowledgeArticleServiceCreatedDomainEvent(link.ArticleId, link.ServiceId));
        return link;
    }
}

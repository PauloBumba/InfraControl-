using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>Vínculo N:N KnowledgeArticle ↔ Incident (F10-T004).</summary>
public sealed class KnowledgeArticleIncident : AggregateRoot
{
    public Guid ArticleId { get; private set; }
    public Guid IncidentId { get; private set; }

    private KnowledgeArticleIncident() { }

    /// <summary>ADR-030 §5 — desvinculação (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new KnowledgeArticleIncidentDeletedDomainEvent(ArticleId, IncidentId));

    public static KnowledgeArticleIncident Create(Guid articleId, Guid incidentId)
    {
        if (articleId == Guid.Empty)
            throw new ArgumentException("O id do artigo é obrigatório.", nameof(articleId));
        if (incidentId == Guid.Empty)
            throw new ArgumentException("O id do incidente é obrigatório.", nameof(incidentId));

        var link = new KnowledgeArticleIncident { ArticleId = articleId, IncidentId = incidentId };
        link.RaiseDomainEvent(new KnowledgeArticleIncidentCreatedDomainEvent(link.ArticleId, link.IncidentId));
        return link;
    }
}

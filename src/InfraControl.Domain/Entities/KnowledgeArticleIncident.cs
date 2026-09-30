using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>Vínculo N:N KnowledgeArticle ↔ Incident (F10-T004).</summary>
public sealed class KnowledgeArticleIncident : Entity
{
    public Guid ArticleId { get; private set; }
    public Guid IncidentId { get; private set; }

    private KnowledgeArticleIncident() { }

    public static KnowledgeArticleIncident Create(Guid articleId, Guid incidentId)
    {
        if (articleId == Guid.Empty)
            throw new ArgumentException("O id do artigo é obrigatório.", nameof(articleId));
        if (incidentId == Guid.Empty)
            throw new ArgumentException("O id do incidente é obrigatório.", nameof(incidentId));

        return new KnowledgeArticleIncident { ArticleId = articleId, IncidentId = incidentId };
    }
}

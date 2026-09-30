using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>Vínculo N:N KnowledgeArticle ↔ Service (F10-T004).</summary>
public sealed class KnowledgeArticleService : Entity
{
    public Guid ArticleId { get; private set; }
    public Guid ServiceId { get; private set; }

    private KnowledgeArticleService() { }

    public static KnowledgeArticleService Create(Guid articleId, Guid serviceId)
    {
        if (articleId == Guid.Empty)
            throw new ArgumentException("O id do artigo é obrigatório.", nameof(articleId));
        if (serviceId == Guid.Empty)
            throw new ArgumentException("O id do serviço é obrigatório.", nameof(serviceId));

        return new KnowledgeArticleService { ArticleId = articleId, ServiceId = serviceId };
    }
}

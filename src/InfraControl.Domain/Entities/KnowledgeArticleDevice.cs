using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>Vínculo N:N KnowledgeArticle ↔ Device (F10-T004).</summary>
public sealed class KnowledgeArticleDevice : Entity
{
    public Guid ArticleId { get; private set; }
    public Guid DeviceId { get; private set; }

    private KnowledgeArticleDevice() { }

    public static KnowledgeArticleDevice Create(Guid articleId, Guid deviceId)
    {
        if (articleId == Guid.Empty)
            throw new ArgumentException("O id do artigo é obrigatório.", nameof(articleId));
        if (deviceId == Guid.Empty)
            throw new ArgumentException("O id do device é obrigatório.", nameof(deviceId));

        return new KnowledgeArticleDevice { ArticleId = articleId, DeviceId = deviceId };
    }
}

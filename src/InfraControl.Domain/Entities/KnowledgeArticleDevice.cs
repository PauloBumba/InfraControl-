using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>Vínculo N:N KnowledgeArticle ↔ Device (F10-T004).</summary>
public sealed class KnowledgeArticleDevice : AggregateRoot
{
    public Guid ArticleId { get; private set; }
    public Guid DeviceId { get; private set; }

    private KnowledgeArticleDevice() { }

    /// <summary>ADR-030 §5 — desvinculação (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new KnowledgeArticleDeviceDeletedDomainEvent(ArticleId, DeviceId));

    public static KnowledgeArticleDevice Create(Guid articleId, Guid deviceId)
    {
        if (articleId == Guid.Empty)
            throw new ArgumentException("O id do artigo é obrigatório.", nameof(articleId));
        if (deviceId == Guid.Empty)
            throw new ArgumentException("O id do device é obrigatório.", nameof(deviceId));

        var link = new KnowledgeArticleDevice { ArticleId = articleId, DeviceId = deviceId };
        link.RaiseDomainEvent(new KnowledgeArticleDeviceCreatedDomainEvent(link.ArticleId, link.DeviceId));
        return link;
    }
}

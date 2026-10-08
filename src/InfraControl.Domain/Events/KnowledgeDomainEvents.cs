using InfraControl.Domain.Common.Events;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Events;

// ADR-030 §5 — eventos CRUD de Knowledge (artigo, categoria e vínculos).

public sealed class KnowledgeArticleCreatedDomainEvent : DomainEvent
{
    public KnowledgeArticleCreatedDomainEvent(Guid articleId, string title, KnowledgeArticleStatus status, Guid? categoryId)
    {
        ArticleId = articleId;
        Title = title;
        Status = status;
        CategoryId = categoryId;
    }

    public Guid ArticleId { get; }
    public string Title { get; }
    public KnowledgeArticleStatus Status { get; }
    public Guid? CategoryId { get; }
    public override string EventType => nameof(KnowledgeArticleCreatedDomainEvent);
}

public sealed class KnowledgeArticleUpdatedDomainEvent : DomainEvent
{
    public KnowledgeArticleUpdatedDomainEvent(Guid articleId, string title, KnowledgeArticleStatus status)
    {
        ArticleId = articleId;
        Title = title;
        Status = status;
    }

    public Guid ArticleId { get; }
    public string Title { get; }
    public KnowledgeArticleStatus Status { get; }
    public override string EventType => nameof(KnowledgeArticleUpdatedDomainEvent);
}

public sealed class KnowledgeArticleDeletedDomainEvent : DomainEvent
{
    public KnowledgeArticleDeletedDomainEvent(Guid articleId, string title)
    {
        ArticleId = articleId;
        Title = title;
    }

    public Guid ArticleId { get; }
    public string Title { get; }
    public override string EventType => nameof(KnowledgeArticleDeletedDomainEvent);
}

public sealed class KnowledgeCategoryCreatedDomainEvent : DomainEvent
{
    public KnowledgeCategoryCreatedDomainEvent(Guid knowledgeCategoryId, string name)
    {
        KnowledgeCategoryId = knowledgeCategoryId;
        Name = name;
    }

    public Guid KnowledgeCategoryId { get; }
    public string Name { get; }
    public override string EventType => nameof(KnowledgeCategoryCreatedDomainEvent);
}

public sealed class KnowledgeCategoryUpdatedDomainEvent : DomainEvent
{
    public KnowledgeCategoryUpdatedDomainEvent(Guid knowledgeCategoryId, string name)
    {
        KnowledgeCategoryId = knowledgeCategoryId;
        Name = name;
    }

    public Guid KnowledgeCategoryId { get; }
    public string Name { get; }
    public override string EventType => nameof(KnowledgeCategoryUpdatedDomainEvent);
}

public sealed class KnowledgeCategoryDeletedDomainEvent : DomainEvent
{
    public KnowledgeCategoryDeletedDomainEvent(Guid knowledgeCategoryId, string name)
    {
        KnowledgeCategoryId = knowledgeCategoryId;
        Name = name;
    }

    public Guid KnowledgeCategoryId { get; }
    public string Name { get; }
    public override string EventType => nameof(KnowledgeCategoryDeletedDomainEvent);
}

public sealed class KnowledgeArticleDeviceCreatedDomainEvent : DomainEvent
{
    public KnowledgeArticleDeviceCreatedDomainEvent(Guid articleId, Guid deviceId)
    {
        ArticleId = articleId;
        DeviceId = deviceId;
    }

    public Guid ArticleId { get; }
    public Guid DeviceId { get; }
    public override string EventType => nameof(KnowledgeArticleDeviceCreatedDomainEvent);
}

public sealed class KnowledgeArticleDeviceDeletedDomainEvent : DomainEvent
{
    public KnowledgeArticleDeviceDeletedDomainEvent(Guid articleId, Guid deviceId)
    {
        ArticleId = articleId;
        DeviceId = deviceId;
    }

    public Guid ArticleId { get; }
    public Guid DeviceId { get; }
    public override string EventType => nameof(KnowledgeArticleDeviceDeletedDomainEvent);
}

// F24-T004/K4 — origem do artigo (docs/32 §9), polimórfica.

public sealed class KnowledgeArticleOriginCreatedDomainEvent : DomainEvent
{
    public KnowledgeArticleOriginCreatedDomainEvent(
        Guid articleId, KnowledgeOriginType originType, Guid originId)
    {
        ArticleId = articleId;
        OriginType = originType;
        OriginId = originId;
    }

    public Guid ArticleId { get; }
    public KnowledgeOriginType OriginType { get; }
    public Guid OriginId { get; }
    public override string EventType => nameof(KnowledgeArticleOriginCreatedDomainEvent);
}

public sealed class KnowledgeArticleOriginDeletedDomainEvent : DomainEvent
{
    public KnowledgeArticleOriginDeletedDomainEvent(
        Guid articleId, KnowledgeOriginType originType, Guid originId)
    {
        ArticleId = articleId;
        OriginType = originType;
        OriginId = originId;
    }

    public Guid ArticleId { get; }
    public KnowledgeOriginType OriginType { get; }
    public Guid OriginId { get; }
    public override string EventType => nameof(KnowledgeArticleOriginDeletedDomainEvent);
}

public sealed class KnowledgeArticleIncidentCreatedDomainEvent : DomainEvent
{
    public KnowledgeArticleIncidentCreatedDomainEvent(Guid articleId, Guid incidentId)
    {
        ArticleId = articleId;
        IncidentId = incidentId;
    }

    public Guid ArticleId { get; }
    public Guid IncidentId { get; }
    public override string EventType => nameof(KnowledgeArticleIncidentCreatedDomainEvent);
}

public sealed class KnowledgeArticleIncidentDeletedDomainEvent : DomainEvent
{
    public KnowledgeArticleIncidentDeletedDomainEvent(Guid articleId, Guid incidentId)
    {
        ArticleId = articleId;
        IncidentId = incidentId;
    }

    public Guid ArticleId { get; }
    public Guid IncidentId { get; }
    public override string EventType => nameof(KnowledgeArticleIncidentDeletedDomainEvent);
}

public sealed class KnowledgeArticleServiceCreatedDomainEvent : DomainEvent
{
    public KnowledgeArticleServiceCreatedDomainEvent(Guid articleId, Guid serviceId)
    {
        ArticleId = articleId;
        ServiceId = serviceId;
    }

    public Guid ArticleId { get; }
    public Guid ServiceId { get; }
    public override string EventType => nameof(KnowledgeArticleServiceCreatedDomainEvent);
}

public sealed class KnowledgeArticleServiceDeletedDomainEvent : DomainEvent
{
    public KnowledgeArticleServiceDeletedDomainEvent(Guid articleId, Guid serviceId)
    {
        ArticleId = articleId;
        ServiceId = serviceId;
    }

    public Guid ArticleId { get; }
    public Guid ServiceId { get; }
    public override string EventType => nameof(KnowledgeArticleServiceDeletedDomainEvent);
}

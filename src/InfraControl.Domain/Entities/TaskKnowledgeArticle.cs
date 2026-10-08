using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>Vínculo N:N Task ↔ KnowledgeArticle (F23-T005/P5a, docs/32 §10).</summary>
public sealed class TaskKnowledgeArticle : AggregateRoot
{
    public Guid TaskId { get; private set; }
    public Guid ArticleId { get; private set; }

    private TaskKnowledgeArticle() { }

    /// <summary>ADR-030 §5 — desvinculação (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new TaskKnowledgeArticleDeletedDomainEvent(TaskId, ArticleId));

    public static TaskKnowledgeArticle Create(Guid taskId, Guid articleId)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException("O id da task é obrigatório.", nameof(taskId));
        if (articleId == Guid.Empty)
            throw new ArgumentException("O id do artigo é obrigatório.", nameof(articleId));

        var link = new TaskKnowledgeArticle { TaskId = taskId, ArticleId = articleId };
        link.RaiseDomainEvent(new TaskKnowledgeArticleCreatedDomainEvent(
            link.TaskId, link.ArticleId));
        return link;
    }
}

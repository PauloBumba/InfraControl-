using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Artigo da base de conhecimento (F10-T002/T003, docs/04 — Knowledge
/// Base). Nasce <see cref="KnowledgeArticleStatus.Draft"/>; o ciclo de
/// publicação Draft → Published → Archived → Draft vive em
/// <see cref="TransitionTo"/> (F10-T003). Categoria opcional — a FK é
/// SetNull na migration: apagar categoria não apaga nem bloqueia o
/// artigo (decisão da task, T001 não tinha FK).
/// </summary>
public sealed class KnowledgeArticle : AggregateRoot
{
    public const int TitleMaxLength = 200;
    public const int ContentMaxLength = 20_000;

    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public Guid? CategoryId { get; private set; }
    public KnowledgeArticleStatus Status { get; private set; }

    private KnowledgeArticle() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new KnowledgeArticleDeletedDomainEvent(Id, Title));

    public static KnowledgeArticle Create(string title, string content, Guid? categoryId)
    {
        var article = new KnowledgeArticle
        {
            Status = KnowledgeArticleStatus.Draft,
        };
        article.Update(title, content, categoryId);

        // ADR-030 §5 — descarta o Updated transitório do Update interno.
        article.ClearDomainEvents();
        article.RaiseDomainEvent(new KnowledgeArticleCreatedDomainEvent(
            article.Id, article.Title, article.Status, article.CategoryId));
        return article;
    }

    /// <summary>
    /// Atualiza título/conteúdo/categoria. NÃO mexe no Status — mudar
    /// estado é só via <see cref="TransitionTo"/> (F10-T003).
    /// </summary>
    public void Update(string title, string content, Guid? categoryId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "O título do artigo é obrigatório.", nameof(title));

        title = title.Trim();
        if (title.Length > TitleMaxLength)
            throw new ArgumentException(
                $"O título do artigo pode ter no máximo {TitleMaxLength} caracteres.",
                nameof(title));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException(
                "O conteúdo do artigo é obrigatório.", nameof(content));

        content = content.Trim();
        if (content.Length > ContentMaxLength)
            throw new ArgumentException(
                $"O conteúdo do artigo pode ter no máximo {ContentMaxLength} caracteres.",
                nameof(content));

        Title = title;
        Content = content;
        CategoryId = categoryId;

        RaiseDomainEvent(new KnowledgeArticleUpdatedDomainEvent(Id, Title, Status));
    }

    /// <summary>
    /// F10-T003 (Publishing): ciclo válido Draft → Published →
    /// Archived → Draft. Qualquer outra transição (inclusive repetir o
    /// mesmo estado) é conflito — 409 pt-BR com os dois estados.
    /// </summary>
    public void TransitionTo(KnowledgeArticleStatus target)
    {
        if (!IsValidTransition(Status, target))
            throw new InvalidOperationException(
                $"Transição inválida: '{Status}' → '{target}'.");

        Status = target;

        RaiseDomainEvent(new KnowledgeArticleUpdatedDomainEvent(Id, Title, Status));
    }

    private static bool IsValidTransition(
        KnowledgeArticleStatus from,
        KnowledgeArticleStatus target)
        => (from, target) switch
        {
            (KnowledgeArticleStatus.Draft, KnowledgeArticleStatus.Published) => true,
            (KnowledgeArticleStatus.Published, KnowledgeArticleStatus.Archived) => true,
            (KnowledgeArticleStatus.Archived, KnowledgeArticleStatus.Draft) => true,
            _ => false,
        };
}

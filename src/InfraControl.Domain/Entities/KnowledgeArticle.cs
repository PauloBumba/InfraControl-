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
/// F24-T001/K1 (spec docs/32 §5–§6): identificação com tipo (14 —
/// enum), descrição, tags (coleção primitiva → coluna JSON) e
/// responsável; o autor nasce no <see cref="Create"/> (quem criou,
/// servidor) e não muda.
/// </summary>
public sealed class KnowledgeArticle : AggregateRoot
{
    public const int TitleMaxLength = 200;
    public const int ContentMaxLength = 20_000;

    // F24-T001/K1 — limites de identificação (docs/32 §6).
    public const int DescriptionMaxLength = 500;
    public const int TagMaxLength = 50;
    public const int MaxTagsCount = 20;

    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public Guid? CategoryId { get; private set; }
    public KnowledgeArticleStatus Status { get; private set; }

    // F24-T001/K1 — identificação (docs/32 §5–§6). Tipo nulo = sem
    // classificação (artigos antigos; a spec não define default).
    public KnowledgeArticleType? Type { get; private set; }
    public string? Description { get; private set; }
    public Guid? AuthorId { get; private set; }
    public Guid? ResponsibleId { get; private set; }

    // Tags: coleção primitiva do EF (coluna JSON `tags`), normalizadas
    // no domínio (trim + lowercase + dedup). Linhas antigas têm NULL no
    // banco — o getter nunca devolve null.
    private List<string>? _tags;
    public IReadOnlyList<string> Tags => _tags ?? [];

    private KnowledgeArticle() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new KnowledgeArticleDeletedDomainEvent(Id, Title));

    public static KnowledgeArticle Create(
        string title,
        string content,
        Guid? categoryId,
        KnowledgeArticleType? type = null,
        string? description = null,
        IReadOnlyCollection<string>? tags = null,
        Guid? responsibleId = null,
        Guid? authorId = null)
    {
        var article = new KnowledgeArticle
        {
            Status = KnowledgeArticleStatus.Draft,
            // F24-T001/K1: autor = quem criou (definido no servidor).
            AuthorId = authorId,
        };
        article.Update(title, content, categoryId);
        article.SetIdentification(type, description, tags, responsibleId);

        // ADR-030 §5 — descarta o Updated transitório do Update interno.
        article.ClearDomainEvents();
        article.RaiseDomainEvent(new KnowledgeArticleCreatedDomainEvent(
            article.Id, article.Title, article.Status, article.CategoryId));
        return article;
    }

    /// <summary>
    /// F24-T001/K1 (spec docs/32 §6 — identificação): tipo, descrição,
    /// tags e responsável. NÃO mexe no <see cref="AuthorId"/> (é a
    /// autoria, definida no <see cref="Create"/>) e NÃO levanta evento —
    /// os commands chamam junto do <see cref="Update"/>, que já notifica.
    /// </summary>
    public void SetIdentification(
        KnowledgeArticleType? type,
        string? description,
        IReadOnlyCollection<string>? tags,
        Guid? responsibleId)
    {
        Type = type;
        Description = NormalizeDescription(description);
        _tags = NormalizeTags(tags);
        ResponsibleId = responsibleId;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        description = description.Trim();
        if (description.Length > DescriptionMaxLength)
            throw new ArgumentException(
                $"A descrição do artigo pode ter no máximo {DescriptionMaxLength} caracteres.",
                nameof(description));

        return description;
    }

    /// <summary>
    /// Trim + lowercase + remoção de vazios e duplicatas (na ordem de
    /// chegada); cada tag ≤ <see cref="TagMaxLength"/> e no máximo
    /// <see cref="MaxTagsCount"/> — tudo em pt-BR (ArgumentException →
    /// 400 pelo handler).
    /// </summary>
    public static List<string> NormalizeTags(IReadOnlyCollection<string>? tags)
    {
        if (tags is null || tags.Count == 0)
            return [];

        var normalized = new List<string>();
        foreach (var raw in tags)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var tag = raw.Trim().ToLowerInvariant();
            if (tag.Length > TagMaxLength)
                throw new ArgumentException(
                    $"Cada tag pode ter no máximo {TagMaxLength} caracteres.",
                    nameof(tags));
            if (!normalized.Contains(tag))
                normalized.Add(tag);
        }

        if (normalized.Count > MaxTagsCount)
            throw new ArgumentException(
                $"O artigo pode ter no máximo {MaxTagsCount} tags.",
                nameof(tags));

        return normalized;
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

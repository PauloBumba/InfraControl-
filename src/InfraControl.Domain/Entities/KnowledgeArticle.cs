using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Artigo da base de conhecimento (F10-T002/T003, docs/04 — Knowledge
/// Base; tipos e identificação F24-T001/K1 — docs/32 §5–§6; workflow de
/// status F24-T002/K2 — ADR-039, docs/32 §7). Nasce
/// <see cref="KnowledgeArticleStatus.Draft"/>; o workflow editorial
/// RASCUNHO → EM REVISÃO → APROVADA → PUBLICADA → EM REVISÃO → ATUALIZADA
/// (+ residuais Arquivada/Obsoleta/Rejeitada) vive em
/// <see cref="TransitionTo"/> (K2). Categoria opcional — a FK é SetNull
/// na migration: apagar categoria não apaga nem bloqueia o artigo
/// (decisão da task, T001 não tinha FK). <c>Type</c> é obrigatório no
/// Create/Update mas a coluna é nullable: artigos legados (anteriores ao
/// K1) nascem sem tipo até a primeira edição.
/// </summary>
public sealed class KnowledgeArticle : AggregateRoot
{
    public const int TitleMaxLength = 200;
    public const int ContentMaxLength = 20_000;
    public const int DescriptionMaxLength = 500;
    public const int TagMaxLength = 30;
    public const int MaxTagsCount = 10;

    /// <summary>Os 14 tipos de docs/32 §5 — conjunto fechado (K1).</summary>
    public static readonly IReadOnlyList<string> KnownTypes =
    [
        "Procedimento",
        "Manual",
        "Guia",
        "FAQ",
        "Solução conhecida",
        "Configuração",
        "Documentação técnica",
        "Boas práticas",
        "Política",
        "Norma interna",
        "Troubleshooting",
        "Conhecimento de sistema",
        "Conhecimento de infraestrutura",
        "Conhecimento operacional",
    ];

    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public Guid? CategoryId { get; private set; }
    public KnowledgeArticleStatus Status { get; private set; }

    // F24-T001/K1 — identificação (docs/32 §6). Type nulo = artigo
    // legado (criado antes do K1); AuthorId vem do usuário autenticado
    // no Create e nunca mais muda.
    public string? Type { get; private set; }
    public string? Description { get; private set; }
    public Guid? AuthorId { get; private set; }
    public Guid? ResponsibleId { get; private set; }
    public List<KnowledgeArticleTag> Tags { get; private set; } = [];

    private KnowledgeArticle() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new KnowledgeArticleDeletedDomainEvent(Id, Title));

    public static KnowledgeArticle Create(
        string title,
        string content,
        Guid? categoryId,
        string type,
        string? description = null,
        IReadOnlyCollection<string>? tags = null,
        Guid? authorId = null,
        Guid? responsibleId = null)
    {
        var article = new KnowledgeArticle
        {
            Status = KnowledgeArticleStatus.Draft,
        };
        article.Update(
            title, content, categoryId, type, description, tags, responsibleId);

        // Autor nasce aqui e é imutável — o Update não o toca.
        article.AuthorId = authorId;

        // ADR-030 §5 — descarta o Updated transitório do Update interno.
        article.ClearDomainEvents();
        article.RaiseDomainEvent(new KnowledgeArticleCreatedDomainEvent(
            article.Id, article.Title, article.Status, article.CategoryId));
        return article;
    }

    /// <summary>
    /// Atualiza título/conteúdo/categoria e a identificação K1 (tipo,
    /// descrição, responsável + substituição das tags). NÃO mexe no
    /// Status — mudar estado é só via <see cref="TransitionTo"/>
    /// (F10-T003) — nem no AuthorId (imutável).
    /// </summary>
    public void Update(
        string title,
        string content,
        Guid? categoryId,
        string type,
        string? description = null,
        IReadOnlyCollection<string>? tags = null,
        Guid? responsibleId = null)
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
        Type = NormalizeType(type);
        Description = NormalizeDescription(description);
        ResponsibleId = responsibleId;
        ReplaceTags(tags);

        RaiseDomainEvent(new KnowledgeArticleUpdatedDomainEvent(Id, Title, Status));
    }

    /// <summary>
    /// F24-T002/K2 (ADR-039 §2): matriz de 12 setas do workflow do §7.
    /// Qualquer outra transição (inclusive repetir o mesmo estado) é
    /// conflito — 409 pt-BR com os rótulos dos dois estados.
    /// </summary>
    public void TransitionTo(KnowledgeArticleStatus target)
    {
        if (!IsValidTransition(Status, target))
            throw new InvalidOperationException(
                $"Transição inválida: '{Status.ToPtBrLabel()}' → '{target.ToPtBrLabel()}'.");

        Status = target;

        RaiseDomainEvent(new KnowledgeArticleUpdatedDomainEvent(Id, Title, Status));
    }

    private static bool IsValidTransition(
        KnowledgeArticleStatus from,
        KnowledgeArticleStatus target)
        => (from, target) switch
        {
            (KnowledgeArticleStatus.Draft, KnowledgeArticleStatus.InReview) => true,
            (KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Approved) => true,
            (KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Updated) => true,
            (KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Rejected) => true,
            (KnowledgeArticleStatus.Approved, KnowledgeArticleStatus.Published) => true,
            (KnowledgeArticleStatus.Published, KnowledgeArticleStatus.InReview) => true,
            (KnowledgeArticleStatus.Published, KnowledgeArticleStatus.Archived) => true,
            (KnowledgeArticleStatus.Published, KnowledgeArticleStatus.Obsolete) => true,
            (KnowledgeArticleStatus.Updated, KnowledgeArticleStatus.InReview) => true,
            (KnowledgeArticleStatus.Updated, KnowledgeArticleStatus.Archived) => true,
            (KnowledgeArticleStatus.Updated, KnowledgeArticleStatus.Obsolete) => true,
            (KnowledgeArticleStatus.Archived, KnowledgeArticleStatus.Draft) => true,
            (KnowledgeArticleStatus.Obsolete, KnowledgeArticleStatus.Draft) => true,
            (KnowledgeArticleStatus.Rejected, KnowledgeArticleStatus.Draft) => true,
            _ => false,
        };

    // ── F24-T001/K1 — identificação ─────────────────────────────────────

    /// <summary>Obrigatório e do conjunto fechado de <see cref="KnownTypes"/>
    /// (case-insensitive → valor canônico); fora = 400 pt-BR listando as
    /// opções (precedente: status do F10-T003).</summary>
    private static string NormalizeType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException(
                "O tipo do artigo é obrigatório.", nameof(type));

        var trimmed = type.Trim();
        var canonical = KnownTypes.FirstOrDefault(
            known => string.Equals(known, trimmed, StringComparison.OrdinalIgnoreCase));
        if (canonical is null)
            throw new ArgumentException(
                $"Tipo inválido: '{trimmed}'. Use: " +
                string.Join(", ", KnownTypes.Select(known => $"'{known}'")) +
                ".",
                nameof(type));

        return canonical;
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

    private static List<string> NormalizeTags(IReadOnlyCollection<string>? tags)
    {
        if (tags is null || tags.Count == 0)
            return [];

        if (tags.Count > MaxTagsCount)
            throw new ArgumentException(
                $"O artigo pode ter no máximo {MaxTagsCount} tags.",
                nameof(tags));

        var normalized = new List<string>(tags.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in tags)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new ArgumentException(
                    "A tag não pode ser vazia.", nameof(tags));

            var tag = raw.Trim();
            if (tag.Length > TagMaxLength)
                throw new ArgumentException(
                    $"A tag pode ter no máximo {TagMaxLength} caracteres.",
                    nameof(tags));

            var lower = tag.ToLowerInvariant();
            if (seen.Add(lower))
                normalized.Add(lower);
        }

        return normalized;
    }

    /// <summary>
    /// Substituição no PUT: remove as que saíram, mantém as iguais
    /// (mesma instância — evita conflito de chave no EF) e acrescenta as
    /// novas. Normalização (trim/lowercase/dedupe) em <see cref="NormalizeTags"/>.
    /// </summary>
    private void ReplaceTags(IReadOnlyCollection<string>? tags)
    {
        var wanted = NormalizeTags(tags);

        for (var i = Tags.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(Tags[i].Tag))
                Tags.RemoveAt(i);
        }

        foreach (var tag in wanted)
        {
            if (!Tags.Any(existing => existing.Tag == tag))
                Tags.Add(KnowledgeArticleTag.Create(Id, tag));
        }
    }
}

using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Artigo da base de conhecimento (F10-T002/T003, docs/04 — Knowledge
<<<<<<< HEAD
/// Base). Nasce <see cref="KnowledgeArticleStatus.Draft"/>; o workflow
/// de revisão (8 estados — F24-T002/K2, ADR-034) vive em
/// <see cref="TransitionTo"/>. Categoria opcional — a FK é
/// SetNull na migration: apagar categoria não apaga nem bloqueia o
/// artigo (decisão da task, T001 não tinha FK).
/// F24-T001/K1 (spec docs/32 §5–§6): identificação com tipo (14 —
/// enum), descrição, tags (coleção primitiva → coluna JSON) e
/// responsável; o autor nasce no <see cref="Create"/> (quem criou,
/// servidor) e não muda.
=======
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
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
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

    // F24-T001/K1 — limites de identificação (docs/32 §6).
    public const int DescriptionMaxLength = 500;
    public const int TagMaxLength = 50;
    public const int MaxTagsCount = 20;

    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public Guid? CategoryId { get; private set; }
    public KnowledgeArticleStatus Status { get; private set; }

<<<<<<< HEAD
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

    // F24-T003/K3 (spec docs/32 §8): versão corrente ("1.0" na
    // criação) e o major que a próxima edição abrirá depois de uma
    // aprovação (piso — coluna `version_major_floor`).
    public string Version { get; private set; } = "1.0";
    private int _versionMajorFloor = 1;
=======
    // F24-T001/K1 — identificação (docs/32 §6). Type nulo = artigo
    // legado (criado antes do K1); AuthorId vem do usuário autenticado
    // no Create e nunca mais muda.
    public string? Type { get; private set; }
    public string? Description { get; private set; }
    public Guid? AuthorId { get; private set; }
    public Guid? ResponsibleId { get; private set; }
    public List<KnowledgeArticleTag> Tags { get; private set; } = [];
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c

    private KnowledgeArticle() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new KnowledgeArticleDeletedDomainEvent(Id, Title));

    public static KnowledgeArticle Create(
        string title,
        string content,
        Guid? categoryId,
<<<<<<< HEAD
        KnowledgeArticleType? type = null,
        string? description = null,
        IReadOnlyCollection<string>? tags = null,
        Guid? responsibleId = null,
        Guid? authorId = null)
=======
        string type,
        string? description = null,
        IReadOnlyCollection<string>? tags = null,
        Guid? authorId = null,
        Guid? responsibleId = null)
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
    {
        var article = new KnowledgeArticle
        {
            Status = KnowledgeArticleStatus.Draft,
            // F24-T001/K1: autor = quem criou (definido no servidor).
            AuthorId = authorId,
        };
<<<<<<< HEAD
        article.Update(title, content, categoryId);
        article.SetIdentification(type, description, tags, responsibleId);
=======
        article.Update(
            title, content, categoryId, type, description, tags, responsibleId);

        // Autor nasce aqui e é imutável — o Update não o toca.
        article.AuthorId = authorId;
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c

        // ADR-030 §5 — descarta o Updated transitório do Update interno.
        article.ClearDomainEvents();
        article.RaiseDomainEvent(new KnowledgeArticleCreatedDomainEvent(
            article.Id, article.Title, article.Status, article.CategoryId));
        return article;
    }

    /// <summary>
<<<<<<< HEAD
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

    // ── F24-T003/K3 — numeração de versões (spec docs/32 §8) ────────────

    /// <summary>
    /// Próxima versão: criação = 1.0; cada edição abre minor (1.1,
    /// 1.2…); depois de uma aprovação a próxima edição abre o major do
    /// piso (2.0, 3.0…). A cadeia da §8 (v1.0 → v1.1 → v2.0) sai daí.
    /// </summary>
    public void BumpVersion()
    {
        var parts = Version.Split('.');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var major)
            || !int.TryParse(parts[1], out var minor))
            throw new InvalidOperationException(
                $"Número de versão inválido: '{Version}'.");

        Version = _versionMajorFloor > major
            ? $"{_versionMajorFloor}.0"
            : $"{major}.{minor + 1}";
    }

    /// <summary>
    /// Ao aprovar (ADR-034), a próxima edição começa um novo major —
    /// aprovado em 1.2 → a próxima edição é 2.0. Re-aprovar só eleva o
    /// piso se o major corrente ainda for menor.
    /// </summary>
    public void MarkVersionApproved()
    {
        var major = int.Parse(Version.Split('.')[0]);
        _versionMajorFloor = Math.Max(_versionMajorFloor, major + 1);
    }

    // ── F24-T005/K5 — revisão periódica (spec docs/32 §6/§12) ──────────
    //
    // Governança da §6: "próxima revisão" + "responsável pela revisão";
    // §12: "Revisar Knowledge a cada 180 dias" → intervalo por artigo.
    // Invariante: NextReviewAt não-nulo ⟺ ReviewIntervalDays não-nulo
    // (só o intervalo calcula a data — setar intervalo null limpa).

    public const int MinReviewIntervalDays = 1;
    public const int MaxReviewIntervalDays = 3650;

    public DateTime? NextReviewAt { get; private set; }
    public int? ReviewIntervalDays { get; private set; }
    public Guid? ReviewResponsibleId { get; private set; }

    /// <summary>
    /// Configura/limpa a revisão periódica: próxima revisão = hoje +
    /// intervalo. Intervalo fora de 1..3650 = 400 pt-BR (a limpeza, com
    /// null, passa direto).
    /// </summary>
    public void SetReviewSchedule(int? intervalDays, Guid? reviewResponsibleId)
    {
        if (intervalDays is < MinReviewIntervalDays or > MaxReviewIntervalDays)
            throw new ArgumentException(
                $"O intervalo de revisão deve estar entre {MinReviewIntervalDays} "
                + $"e {MaxReviewIntervalDays} dias.",
                nameof(intervalDays));

        ReviewIntervalDays = intervalDays;
        ReviewResponsibleId = reviewResponsibleId;
        NextReviewAt = intervalDays is { } days
            ? AppTimeProvider.Current.GetUtcNow().UtcDateTime.AddDays(days)
            : null;
    }

    /// <summary>
    /// Ação "manter" da §12: a revisão foi feita sem mudar o conteúdo →
    /// reagenda a próxima revisão. Sem intervalo = 409 pt-BR.
    /// </summary>
    public void ConfirmReview()
    {
        if (ReviewIntervalDays is not int interval)
            throw new InvalidOperationException(
                "O artigo não tem revisão periódica configurada.");

        NextReviewAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime.AddDays(interval);
    }

    /// <summary>
    /// Ação "atualizar" da §12: editar o artigo satisfaz a revisão →
    /// reagenda (no-op quando não há intervalo configurado).
    /// </summary>
    public void PostponeReview()
    {
        if (ReviewIntervalDays is int interval)
            NextReviewAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime.AddDays(interval);
    }

    /// <summary>
    /// Atualiza título/conteúdo/categoria. NÃO mexe no Status — mudar
    /// estado é só via <see cref="TransitionTo"/> (F10-T003).
=======
    /// Atualiza título/conteúdo/categoria e a identificação K1 (tipo,
    /// descrição, responsável + substituição das tags). NÃO mexe no
    /// Status — mudar estado é só via <see cref="TransitionTo"/>
    /// (F10-T003) — nem no AuthorId (imutável).
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
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
<<<<<<< HEAD
    /// F24-T002/K2 (ADR-034, spec docs/32 §7): matriz de revisão —
    /// <c>Draft → InReview → Approved → Published</c> (publicação com
    /// aprovação), <c>Published → InReview → Updated</c> (revisão do
    /// publicado), <c>Published/Updated → Archived/Obsolete</c> e
    /// <c>Archived/Obsolete/Rejected → Draft</c> (restauração).
    /// Qualquer outra transição (inclusive repetir o mesmo estado) é
    /// conflito — 409 pt-BR com os dois estados.
=======
    /// F24-T002/K2 (ADR-039 §2): matriz de 12 setas do workflow do §7.
    /// Qualquer outra transição (inclusive repetir o mesmo estado) é
    /// conflito — 409 pt-BR com os rótulos dos dois estados.
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
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
<<<<<<< HEAD
            // Cadeia da §7 (rascunho → revisão → aprovação → publicação).
=======
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
            (KnowledgeArticleStatus.Draft, KnowledgeArticleStatus.InReview) => true,
            (KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Approved) => true,
            (KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Updated) => true,
            (KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Rejected) => true,
<<<<<<< HEAD
            (KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Draft) => true,
            (KnowledgeArticleStatus.Approved, KnowledgeArticleStatus.Published) => true,
            (KnowledgeArticleStatus.Approved, KnowledgeArticleStatus.Draft) => true,

            // Revisão do publicado + especiais.
=======
            (KnowledgeArticleStatus.Approved, KnowledgeArticleStatus.Published) => true,
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
            (KnowledgeArticleStatus.Published, KnowledgeArticleStatus.InReview) => true,
            (KnowledgeArticleStatus.Published, KnowledgeArticleStatus.Archived) => true,
            (KnowledgeArticleStatus.Published, KnowledgeArticleStatus.Obsolete) => true,
            (KnowledgeArticleStatus.Updated, KnowledgeArticleStatus.InReview) => true,
            (KnowledgeArticleStatus.Updated, KnowledgeArticleStatus.Archived) => true,
            (KnowledgeArticleStatus.Updated, KnowledgeArticleStatus.Obsolete) => true,
<<<<<<< HEAD

            // Estados de saída restauram/retrabalham no rascunho.
            (KnowledgeArticleStatus.Archived, KnowledgeArticleStatus.Draft) => true,
            (KnowledgeArticleStatus.Obsolete, KnowledgeArticleStatus.Draft) => true,
            (KnowledgeArticleStatus.Rejected, KnowledgeArticleStatus.Draft) => true,

=======
            (KnowledgeArticleStatus.Archived, KnowledgeArticleStatus.Draft) => true,
            (KnowledgeArticleStatus.Obsolete, KnowledgeArticleStatus.Draft) => true,
            (KnowledgeArticleStatus.Rejected, KnowledgeArticleStatus.Draft) => true,
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
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

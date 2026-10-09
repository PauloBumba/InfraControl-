using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.Domain.Common;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Knowledge.Articles;

/// <summary>
/// Payload de KnowledgeArticle devolvido pela API (F10-T002/T004;
<<<<<<< HEAD
/// identificação F24-T001/K1 — docs/32 §5–§6). Os ids dos vínculos
/// (F10-T004) só vêm no GET de detalhe — a busca não detalha (evita
/// N+1 por página).
=======
/// tipos e identificação K1 — F24-T001). Os ids dos vínculos (F10-T004)
/// só vêm no GET de detalhe — a busca não detalha (evita N+1 por página).
/// Nomes de autor/responsável são resolvidos só nas queries Search/Get
/// (join em DomainUsers, mesmo precedente de Tasks/PDI).
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
/// </summary>
public sealed record KnowledgeArticleDto(
    Guid Id,
    string Title,
    string Content,
    Guid? CategoryId,
    KnowledgeArticleStatus Status,
    string? Type = null,
    string? Description = null,
    Guid? AuthorId = null,
    Guid? ResponsibleId = null)
{
    public IReadOnlyCollection<string> Tags { get; init; } = [];
    public IReadOnlyCollection<Guid> DeviceIds { get; init; } = [];
    public IReadOnlyCollection<Guid> IncidentIds { get; init; } = [];
    public IReadOnlyCollection<Guid> ServiceIds { get; init; } = [];
<<<<<<< HEAD

    // F24-T004/K4 — origens (spec docs/32 §9): tipo + id
    // ("Task"/"Pdi"/"Incident" — §9 desenha "Task #0042"). Vem em
    // lote na busca (round-trip único, sem N+1) e no detalhe.
    public IReadOnlyCollection<KnowledgeArticleOriginDto> Origins { get; init; } = [];

    // F24-T001/K1 — identificação (spec docs/32 §5–§6). `Type` é o
    // nome do enum em string (rótulo pt-BR fica no catálogo do front);
    // null = sem tipo. Nomes resolvidos no backend — o front não tem
    // como resolver sem a policy do /admin/users (leitores ficam de
    // fora).
    public string? Type { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public Guid? AuthorId { get; init; }
    public string? AuthorName { get; init; }
    public Guid? ResponsibleId { get; init; }
    public string? ResponsibleName { get; init; }

    // F24-T003/K3 — versão corrente do artigo ("1.0" na criação;
    // docs/32 §8). O histórico completo fica em GET {id}/versions.
    public string Version { get; init; } = "1.0";

    // F24-T005/K5 — governança (spec docs/32 §6 + §12): próxima
    // revisão, intervalo configurado (ex.: 180 dias) e responsável
    // pela revisão (nome resolvido no mesmo batch do autor).
    public DateTime? NextReviewAt { get; init; }
    public int? ReviewIntervalDays { get; init; }
    public Guid? ReviewResponsibleId { get; init; }
    public string? ReviewResponsibleName { get; init; }

    /// <summary>
    /// Alerta da §12 — "Knowledge necessita de revisão": próxima
    /// revisão vencida (comparada ao relógio do app).
    /// </summary>
    public bool PendingReview { get; init; }

    /// <summary>
    /// Leitura a partir da entidade + nomes resolvidos (join em
    /// DomainUsers). Vínculos (device/incident/service) continuam só no
    /// detalhe, aplicados com `with` pelo handler.
    /// </summary>
    public static KnowledgeArticleDto From(
        KnowledgeArticle article,
        IReadOnlyDictionary<Guid, string>? userNames = null)
    {
        static string? NameOf(IReadOnlyDictionary<Guid, string>? names, Guid? id)
            => id is { } key && names is not null && names.TryGetValue(key, out var name)
                ? name
                : null;

        return new KnowledgeArticleDto(
            article.Id, article.Title, article.Content,
            article.CategoryId, article.Status)
        {
            Type = article.Type?.ToString(),
            Description = article.Description,
            Tags = article.Tags,
            AuthorId = article.AuthorId,
            AuthorName = NameOf(userNames, article.AuthorId),
            ResponsibleId = article.ResponsibleId,
            ResponsibleName = NameOf(userNames, article.ResponsibleId),
            Version = article.Version,
            NextReviewAt = article.NextReviewAt,
            ReviewIntervalDays = article.ReviewIntervalDays,
            ReviewResponsibleId = article.ReviewResponsibleId,
            ReviewResponsibleName = NameOf(userNames, article.ReviewResponsibleId),
            PendingReview = article.NextReviewAt is { } due
                && due <= AppTimeProvider.Current.GetUtcNow().UtcDateTime,
        };
    }

    /// <summary>
    /// Resolve autor/responsável/responsável pela revisão num único
    /// round-trip — usada pelos handlers de leitura/escrita de um
    /// único artigo.
    /// </summary>
    internal static async Task<KnowledgeArticleDto> FromAsync(
        KnowledgeArticle article,
        IKnowledgeArticleRepository repository,
        CancellationToken cancellationToken)
    {
        var ids = NameIds(article);
        var names = ids.Length == 0
            ? null
            : await repository.GetUserNamesAsync(ids, cancellationToken);
        return From(article, names);
    }

    /// <summary>Ids de usuário (autor + responsável + revisor) a resolver, sem duplicata.</summary>
    internal static Guid[] NameIds(KnowledgeArticle article)
        => [.. new[]
        {
            article.AuthorId,
            article.ResponsibleId,
            article.ReviewResponsibleId,
        }.OfType<Guid>().Distinct()];
=======
    public string? AuthorName { get; init; }
    public string? ResponsibleName { get; init; }
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
}

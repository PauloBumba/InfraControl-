using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Knowledge.Articles;

/// <summary>
/// Payload de KnowledgeArticle devolvido pela API (F10-T002/T004;
/// identificação F24-T001/K1 — docs/32 §5–§6). Os ids dos vínculos
/// (F10-T004) só vêm no GET de detalhe — a busca não detalha (evita
/// N+1 por página).
/// </summary>
public sealed record KnowledgeArticleDto(
    Guid Id,
    string Title,
    string Content,
    Guid? CategoryId,
    KnowledgeArticleStatus Status)
{
    public IReadOnlyCollection<Guid> DeviceIds { get; init; } = [];
    public IReadOnlyCollection<Guid> IncidentIds { get; init; } = [];
    public IReadOnlyCollection<Guid> ServiceIds { get; init; } = [];

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
        };
    }

    /// <summary>
    /// Resolve autor/responsável num único round-trip (dois ids) — usada
    /// pelos handlers de leitura/escrita de um único artigo.
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

    /// <summary>Ids de usuário (autor + responsável) a resolver, sem duplicata.</summary>
    internal static Guid[] NameIds(KnowledgeArticle article)
        => [.. new[] { article.AuthorId, article.ResponsibleId }.OfType<Guid>().Distinct()];
}

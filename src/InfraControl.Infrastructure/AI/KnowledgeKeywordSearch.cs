using InfraControl.Application.AI.Context;
using InfraControl.Application.Knowledge.Articles.Ports;

namespace InfraControl.Infrastructure.AI;

/// <summary>
/// Busca keyword da híbrida v1 (FAI-T009/S8 · ADR-038 §2 · docs/34 §17):
/// reaproveita o <c>term</c> do K7 sobre artigos <b>Published</b>
/// (<c>includeUnpublished: false</c> — fonte curada inicial do ADR-038
/// §3). Score = posição do rank (1.0, 0.9, …) — heurística simples,
/// sem BM25 nesta fase; o merge vetorial sobrescreve quando houver
/// cosseno melhor.
/// </summary>
public sealed class KnowledgeKeywordSearch(IKnowledgeArticleRepository repository) : IKeywordSearch
{
    public async Task<IReadOnlyList<RetrievalHit>> SearchAsync(
        string term,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var page = await repository.SearchAsync(
            term,
            page: 1,
            pageSize: Math.Max(1, limit),
            includeUnpublished: false,
            pendingReview: false,
            cancellationToken: cancellationToken);

        var hits = page.Items
            .Select((article, index) => new RetrievalHit(
                Id: article.Id.ToString(),
                Text: article.Content ?? string.Empty,
                SourceType: "Knowledge",
                Score: 1.0 - (index * 0.1),
                OccurredAtUtc: article.CreateDateTime,
                Metadata: new Dictionary<string, string>
                {
                    ["title"] = article.Title,
                    ["articleId"] = article.Id.ToString(),
                }))
            .ToList();

        return hits;
    }
}

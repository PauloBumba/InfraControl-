using Microsoft.Extensions.Logging;

namespace InfraControl.Application.AI.Context;

/// <summary>
/// Busca híbrida v1 (FAI-T009/S8 · ADR-038 §2 · docs/34 §17):
/// vetor (cosseno, via <see cref="IVectorStore"/>) + keyword (K7 sobre
/// artigos Published) → merge por Id (melhor score vence) → rerank
/// heurístico → corte no limite.
///
/// Sem vector store registrado (ambiente sem Docker/pgvector —
/// ADR-038 §1) a metade vetorial DEGRADA (§44): log + keyword puro,
/// nunca erro e nunca dado inventado.
/// </summary>
public sealed class KnowledgeHybridRetrievalService(
    IEmbeddingProvider embeddingProvider,
    IVectorStore? vectorStore,
    IKeywordSearch keywordSearch,
    IReranker reranker,
    ILogger<KnowledgeHybridRetrievalService> logger) : IRetrievalService
{
    public async Task<RetrievalResult> RetrieveAsync(
        RetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Term);
        var limit = Math.Max(1, request.Limit);

        var merged = new Dictionary<string, RetrievalHit>(StringComparer.OrdinalIgnoreCase);

        // Metade vetorial (§17) — degradada sem store.
        if (vectorStore is not null)
        {
            try
            {
                var embedding = await embeddingProvider.EmbedAsync(request.Term, cancellationToken);
                var matches = await vectorStore.SearchAsync(
                    new VectorQuery(embedding, limit, request.Filters),
                    cancellationToken);

                foreach (var match in matches)
                {
                    merged[match.Id] = new RetrievalHit(
                        match.Id,
                        match.Text ?? string.Empty,
                        SourceType: "Knowledge",
                        Score: match.Score,
                        Metadata: match.Metadata);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Busca vetorial degradada (§44) — seguindo só com keyword para '{Term}'.",
                    request.Term);
            }
        }
        else
        {
            logger.LogDebug(
                "Vector store não configurado (§44 — pgvector pendente de ambiente); keyword apenas para '{Term}'.",
                request.Term);
        }

        // Metade keyword (§17 — reutiliza o term do K7, Published only).
        foreach (var hit in await keywordSearch.SearchAsync(request.Term, limit, cancellationToken))
        {
            // No merge, keyword NÃO sobrescreve score vetorial melhor.
            if (!merged.TryGetValue(hit.Id, out var existing) || hit.Score > existing.Score)
                merged[hit.Id] = hit;
        }

        var ranked = await reranker.RankAsync([.. merged.Values], cancellationToken);
        var top = ranked.Take(limit).ToList();

        return new RetrievalResult(top, merged.Count);
    }
}

namespace InfraControl.Application.AI.Context;

/// <summary>
/// Reranker heurístico v1 (FAI-T009/S8 · ADR-038 §2): ordena por score
/// (cosseno/posição do keyword) e desempata por recência — determinístico,
/// sem LLM. Rerank por modelo = evolução com ADR próprio.
/// </summary>
public sealed class HeuristicReranker : IReranker
{
    public Task<IReadOnlyList<RetrievalHit>> RankAsync(
        IReadOnlyList<RetrievalHit> hits,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RetrievalHit> ranked = [.. hits
            .OrderByDescending(hit => hit.Score)
            .ThenByDescending(hit => hit.OccurredAtUtc ?? DateTime.MinValue)];
        return Task.FromResult(ranked);
    }
}

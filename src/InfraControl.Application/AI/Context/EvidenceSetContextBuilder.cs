namespace InfraControl.Application.AI.Context;

/// <summary>
/// Context builder v1 (FAI-T009/S8 · §19/§51): hits viram
/// <see cref="Evidence"/> com confiança <c>Probable</c> — o pipeline
/// NUNCA emite <c>Confirmed</c> sem verificação explícita (§19: hipótese
/// não vira fato) — citação <c>fonte#id</c>, tool e traceId para
/// rastreabilidade; o set sai deduplicado e ordenado por confiança e
/// recência.
/// </summary>
public sealed class EvidenceSetContextBuilder : IContextBuilder
{
    public EvidenceSet Build(
        IReadOnlyList<RetrievalHit> hits,
        string? traceId = null,
        string? tool = null)
    {
        var evidences = hits
            .Select(hit => new Evidence(
                Id: Guid.NewGuid(),
                Source: ParseSource(hit.SourceType),
                SourceType: hit.SourceType,
                TimestampUtc: hit.OccurredAtUtc ?? DateTimeOffset.UtcNow.UtcDateTime,
                Data: hit.Text,
                Confidence: EvidenceConfidence.Probable,
                Citation: $"{hit.SourceType}#{hit.Id}",
                Tool: tool,
                TraceId: traceId))
            .GroupBy(evidence => evidence.Citation, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(evidence => evidence.Confidence)
            .ThenByDescending(evidence => evidence.TimestampUtc)
            .ToList();

        return new EvidenceSet(evidences);
    }

    private static EvidenceSource ParseSource(string sourceType) =>
        Enum.TryParse<EvidenceSource>(sourceType, ignoreCase: true, out var parsed)
            ? parsed
            : EvidenceSource.InfraControl;
}

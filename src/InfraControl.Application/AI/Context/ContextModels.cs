namespace InfraControl.Application.AI.Context;

/// <summary>
/// Fonte de uma evidência (docs/34 §19 — tipos de fonte).
/// </summary>
public enum EvidenceSource
{
    Zabbix,
    Unifi,
    InfraControl,
    Knowledge,
    Task,
    Incident,
    Pdi,
    Tesc,
    Document,
    Api,
}

/// <summary>
/// Classificação de confiança (§19): a IA NUNCA apresenta hipótese como
/// fato confirmado — <c>Confirmed</c> só com verificação explícita
/// (futuro); o pipeline v1 emite no máximo <c>Probable</c>.
/// </summary>
public enum EvidenceConfidence
{
    Unknown,
    Possible,
    Probable,
    Confirmed,
}

/// <summary>Evidência estruturada (§19 — modelo conceitual completo).</summary>
public sealed record Evidence(
    Guid Id,
    EvidenceSource Source,
    string SourceType,
    DateTime TimestampUtc,
    string Data,
    EvidenceConfidence Confidence,
    string Citation,
    string? Tool,
    string? TraceId);

/// <summary>
/// Conjunto de evidências entregue ao LLM (§19/§51): deduplicado e
/// ordenado por confiança (desc) e recência.
/// </summary>
public sealed record EvidenceSet(IReadOnlyList<Evidence> Evidences)
{
    public static EvidenceSet Empty { get; } = new([]);

    public bool IsEmpty => Evidences.Count == 0;
}

/// <summary>Hit de busca híbrida (§17/§53 — dados estruturados).</summary>
public sealed record RetrievalHit(
    string Id,
    string Text,
    string SourceType,
    double Score,
    DateTime? OccurredAtUtc = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>Pedido de busca híbrida (§17 — termo + filtros de metadados).</summary>
public sealed record RetrievalRequest(
    string Term,
    IReadOnlyDictionary<string, string>? Filters = null,
    int Limit = 10);

/// <summary>Resultado da recuperação híbrida (merge vetorial+keyword+metadados).</summary>
public sealed record RetrievalResult(IReadOnlyList<RetrievalHit> Hits, int Total);

/// <summary>Documento vetorial indexado no vector store (tabela própria — ADR-038 §4).</summary>
public sealed record VectorDocument(
    string Id,
    string Text,
    float[] Embedding,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>Match do vector store (score = similaridade cosseno).</summary>
public sealed record VectorMatch(
    string Id,
    double Score,
    string? Text,
    IReadOnlyDictionary<string, string>? Metadata);

/// <summary>Consulta vetorial (embedding + limite + filtros de metadados).</summary>
public sealed record VectorQuery(
    float[] Embedding,
    int Limit,
    IReadOnlyDictionary<string, string>? Filters = null);

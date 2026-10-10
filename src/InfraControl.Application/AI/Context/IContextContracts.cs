namespace InfraControl.Application.AI.Context;

/// <summary>
/// Contratos do Context Engine & Hybrid RAG (FAI-T009/S8 · ADR-038 ·
/// docs/34 §16–§19/§35/§51). Tudo em <c>Application/AI/</c>: domínio e
/// Infrastructure nunca referenciados de fora da camada (ADR-038 §2);
/// o vector store é pgvector no Postgres existente (avaliação da imagem
/// pendente de Docker — sem ele o pipeline degrada, §44, sem invenção).
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>Identificador estável (ex.: <c>ollama-embedding</c>).</summary>
    string Name { get; }

    /// <summary>Vetor de embedding do texto (modelo definido em config).</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>
/// Vector store (ADR-038 §1: pgvector no Postgres atual; implementação
/// vive em Infrastructure e só é registrada quando o ambiente existir —
/// sem Docker o Context Engine roda degradado).
/// </summary>
public interface IVectorStore
{
    Task UpsertAsync(VectorDocument document, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorMatch>> SearchAsync(VectorQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Busca keyword (§17 — metade não-vetorial da híbrida v1: ILIKE/FTS
/// reaproveitando o <c>term</c> do K7 sobre artigos Published).
/// </summary>
public interface IKeywordSearch
{
    Task<IReadOnlyList<RetrievalHit>> SearchAsync(
        string term, int limit, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reranker (ADR-038 §2 — v1 heurística: score, fonte e recência;
/// rerank por LLM = evolução com ADR).
/// </summary>
public interface IReranker
{
    Task<IReadOnlyList<RetrievalHit>> RankAsync(
        IReadOnlyList<RetrievalHit> hits, CancellationToken cancellationToken = default);
}

/// <summary>Recuperação híbrida (§17): vetor + keyword + metadados → merge → rerank.</summary>
public interface IRetrievalService
{
    Task<RetrievalResult> RetrieveAsync(
        RetrievalRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Context builder (§19/§51): transforma hits em <see cref="EvidenceSet"/>
/// com confiança, citação e rastreabilidade.
/// </summary>
public interface IContextBuilder
{
    EvidenceSet Build(
        IReadOnlyList<RetrievalHit> hits,
        string? traceId = null,
        string? tool = null);
}

/// <summary>
/// Ingesta de Knowledge publicado no vector store (ADR-038 §3 — fonte
/// inicial; chunking por artigo com limite documentado no chunker).
/// </summary>
public interface IAiKnowledgeIngestion
{
    /// <summary>Ingesta um artigo Publicado; devolve o nº de chunks indexados.</summary>
    Task<int> IngestArticleAsync(Guid articleId, CancellationToken cancellationToken = default);
}

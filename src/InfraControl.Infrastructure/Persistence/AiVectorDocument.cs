namespace InfraControl.Infrastructure.Persistence;

/// <summary>
/// Documento vetorial do RAG (ADR-038 §4): índice de embeddings dos
/// artigos Published do Knowledge. Vive em tabela própria da infra —
/// o domínio Knowledge não conhece vetor. Não herda de
/// <c>Domain.Common.Entities.Entity</c>: a chave é o id textual do chunk
/// (<c>{articleId}:{chunkIndex}</c>) e não há auditoria de domínio.
/// </summary>
public sealed class AiVectorDocument
{
    /// <summary>Chave do chunk: <c>{articleId}:{chunkIndex}</c>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Texto do chunk indexado.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Embedding serializado <c>[f1,f2,…]</c> → coluna <c>vector(768)</c>.</summary>
    public string Embedding { get; set; } = string.Empty;

    /// <summary>Metadados JSON (source/articleId/title) → coluna <c>jsonb</c>.</summary>
    public string? Metadata { get; set; }
}

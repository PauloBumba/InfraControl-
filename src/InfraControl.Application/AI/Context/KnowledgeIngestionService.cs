using InfraControl.Application.Knowledge.Articles;
using InfraControl.Application.Knowledge.Articles.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.AI.Context;

/// <summary>
/// Ingesta de Knowledge publicado no vector store (FAI-T009/S8 ·
/// ADR-038 §3): chunking por artigo (limite documentado no
/// <see cref="KnowledgeChunker"/>), embedding por chunk e upsert com
/// metadados (source/articleId/title) — o índice vive em tabela própria,
/// o domínio Knowledge não conhece vetor (ADR-038 §4).
///
/// Só aceita artigos <c>Published</c> (fonte curada inicial §3). O
/// disparo automático na publicação (workflow ADR-034) é o próximo
/// passo quando o pgvector existir no ambiente; o command já é
/// chamável hoje (e degrada sem store registrado).
/// </summary>
public sealed class KnowledgeIngestionService(
    IMediator mediator,
    IEmbeddingProvider embeddingProvider,
    IVectorStore? vectorStore,
    ILogger<KnowledgeIngestionService> logger) : IAiKnowledgeIngestion
{
    public async Task<int> IngestArticleAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        var article = await mediator.Send(new GetKnowledgeArticleQuery(articleId), cancellationToken);

        if (article.Status != KnowledgeArticleStatus.Published)
        {
            throw new InvalidOperationException(
                $"Artigo {articleId} está '{article.Status}' — a ingesta de IA aceita apenas artigos publicados (ADR-038 §3).");
        }

        // ADR-038 §46: sem pgvector o store não é registrado e a degradação
        // é PROMETIDA no docstring — retornar 0 com log honesto em vez de
        // estourar NRE (o IVectorStore? torna o host E2E e o real resolvíveis).
        if (vectorStore is null)
        {
            logger.LogWarning(
                "Vector store indisponível (pgvector não registrado, ADR-038 §46) — ingesta do artigo {ArticleId} pulada.",
                articleId);
            return 0;
        }

        var metadata = new Dictionary<string, string>
        {
            ["source"] = "Knowledge",
            ["articleId"] = articleId.ToString(),
            ["title"] = article.Title,
        };

        var chunks = KnowledgeChunker.Split(article.Content);
        var indexed = 0;
        foreach (var chunk in chunks)
        {
            var embedding = await embeddingProvider.EmbedAsync(chunk.Text, cancellationToken);
            await vectorStore.UpsertAsync(
                new VectorDocument(
                    Id: $"{articleId}:{chunk.Index}",
                    Text: chunk.Text,
                    Embedding: embedding,
                    Metadata: metadata),
                cancellationToken);
            indexed++;
        }

        logger.LogInformation(
            "Artigo {ArticleId} ingerido no vector store ({Chunks} chunks, {Characters} caracteres).",
            articleId, indexed, article.Content?.Length ?? 0);

        return indexed;
    }
}

/// <summary>Dispara a ingesta de um artigo publicado (command read-only p/ AI).</summary>
public sealed record IngestKnowledgeArticleForAiCommand(Guid ArticleId) : IQuery<int>;

public sealed class IngestKnowledgeArticleForAiCommandHandler(IAiKnowledgeIngestion ingestion)
    : IRequestHandler<IngestKnowledgeArticleForAiCommand, int>
{
    public Task<int> Handle(
        IngestKnowledgeArticleForAiCommand request,
        CancellationToken cancellationToken) =>
        ingestion.IngestArticleAsync(request.ArticleId, cancellationToken);
}

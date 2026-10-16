using FluentAssertions;
using InfraControl.Application.AI.Context;
using InfraControl.Application.Knowledge.Articles;
using InfraControl.Application.Knowledge.Articles.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MediatR;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// FAI-T009/S8 (ADR-038 · docs/34 §17/§19/§35): Context Engine +
/// RAG foundation — chunker por artigo (limite documentado), busca
/// híbrida v1 (vetor + keyword → merge → rerank heurístico; degrada
/// para keyword §44 sem vector store), EvidenceSet §19 (nunca
/// Confirmed sem verificação) e ingesta de Knowledge publicado.
/// Sem Docker: fakes.
/// </summary>
public sealed class AiContextTests
{
    private sealed class FakeEmbeddingProvider : IEmbeddingProvider
    {
        public string Name => "fake-embedding";
        public List<string> Embedded { get; } = [];

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            Embedded.Add(text);
            return Task.FromResult(new[] { 1f, 0f });
        }
    }

    private sealed class FakeVectorStore : IVectorStore
    {
        public List<VectorDocument> Documents { get; } = [];
        public List<VectorQuery> Queries { get; } = [];
        public List<VectorMatch> Matches { get; set; } = [];

        public Task UpsertAsync(VectorDocument document, CancellationToken cancellationToken = default)
        {
            Documents.Add(document);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<VectorMatch>> SearchAsync(
            VectorQuery query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Task.FromResult<IReadOnlyList<VectorMatch>>(Matches);
        }
    }

    private sealed class FakeKeywordSearch(params RetrievalHit[] hits) : IKeywordSearch
    {
        public Task<IReadOnlyList<RetrievalHit>> SearchAsync(
            string term, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RetrievalHit>>(hits.Take(limit).ToList());
    }

    private static KnowledgeHybridRetrievalService Hybrid(
        IVectorStore? store,
        IKeywordSearch keyword,
        FakeEmbeddingProvider? embedding = null) =>
        new(
            embedding ?? new FakeEmbeddingProvider(),
            store,
            keyword,
            new HeuristicReranker(),
            NullLogger<KnowledgeHybridRetrievalService>.Instance);

    // ── Chunker ────────────────────────────────────────────────────────

    [Fact]
    public void Chunker_separa_por_paragrafo_com_indice_sequencial()
    {
        var chunks = KnowledgeChunker.Split("primeiro parágrafo\n\nsegundo parágrafo");

        chunks.Should().HaveCount(2);
        chunks[0].Should().Be(new KnowledgeChunk(0, "primeiro parágrafo"));
        chunks[1].Should().Be(new KnowledgeChunk(1, "segundo parágrafo"));
    }

    [Fact]
    public void Chunker_parte_paragrafo_grande_no_limite_1500()
    {
        var bigParagraph = string.Join(' ', Enumerable.Range(1, 600).Select(index => $"palavra{index}"));

        var chunks = KnowledgeChunker.Split(bigParagraph);

        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().OnlyContain(chunk => chunk.Text.Length <= KnowledgeChunker.MaxChunkCharacters);
        string.Concat(chunks.Select(chunk => chunk.Text)).Replace(" ", string.Empty)
            .Should().Be(bigParagraph.Replace(" ", string.Empty), "nada de conteúdo perdido no corte");
    }

    [Fact]
    public void Chunker_conteudo_vazio_devolve_lista_vazia()
    {
        KnowledgeChunker.Split("   ").Should().BeEmpty();
    }

    // ── Rerank + busca híbrida ─────────────────────────────────────────

    [Fact]
    public async Task Hybrid_sem_vector_store_degrada_para_keyword_44()
    {
        var keywordHit = new RetrievalHit("a1", "texto", "Knowledge", Score: 0.9);

        var result = await Hybrid(store: null, new FakeKeywordSearch(keywordHit))
            .RetrieveAsync(new RetrievalRequest("zabbix"));

        result.Hits.Should().ContainSingle(hit => hit.Id == "a1");
        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task Hybrid_merge_por_id_com_melhor_score_e_ordena_por_rerank()
    {
        var store = new FakeVectorStore
        {
            Matches =
            [
                new VectorMatch("a1", 0.95, "vetor do a1", null),
                new VectorMatch("b1", 0.55, "vetor do b1", null),
            ],
        };
        var keyword = new FakeKeywordSearch(
            new RetrievalHit("a1", "keyword do a1", "Knowledge", Score: 0.8),
            new RetrievalHit("c1", "keyword do c1", "Knowledge", Score: 0.7));

        var result = await Hybrid(store, keyword).RetrieveAsync(new RetrievalRequest("servidor"));

        result.Total.Should().Be(3, "merge deduplica por id");
        // rerank por score (a1=0.95, c1=0.7, b1=0.55)
        result.Hits.Select(hit => hit.Id).Should().Equal("a1", "c1", "b1");
        result.Hits.First(hit => hit.Id == "a1").Score.Should().Be(0.95, "score vetorial melhor prevalece");
        result.Hits.First(hit => hit.Id == "a1").Text.Should().Be("vetor do a1");
    }

    [Fact]
    public async Task Hybrid_falha_no_embedding_degrada_sem_derrubar()
    {
        var embedding = new FakeEmbeddingProvider();
        var brokenStore = new Mock<IVectorStore>();
        brokenStore
            .Setup(store => store.SearchAsync(It.IsAny<VectorQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("pgvector fora"));

        var result = await Hybrid(brokenStore.Object, new FakeKeywordSearch(
                new RetrievalHit("k1", "keyword", "Knowledge", 0.6)), embedding)
            .RetrieveAsync(new RetrievalRequest("rede"));

        result.Hits.Should().ContainSingle(hit => hit.Id == "k1");
    }

    [Fact]
    public async Task Hybrid_respeita_limit()
    {
        var hits = Enumerable.Range(1, 10)
            .Select(index => new RetrievalHit($"h{index}", "texto", "Knowledge", Score: index / 10d))
            .ToArray();

        var result = await Hybrid(null, new FakeKeywordSearch(hits))
            .RetrieveAsync(new RetrievalRequest("x", Limit: 3));

        result.Hits.Should().HaveCount(3);
    }

    // ── EvidenceSet §19 ────────────────────────────────────────────────

    [Fact]
    public void ContextBuilder_gera_evidence_probable_com_citacao_e_trace()
    {
        var builder = new EvidenceSetContextBuilder();
        var hits = new List<RetrievalHit>
        {
            new("a1", "conteúdo", "Knowledge", 0.9, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)),
            new("a1", "duplicado", "Knowledge", 0.8),
        };

        var set = builder.Build(hits, traceId: "trace-9", tool: "search_knowledge");

        var evidence = set.Evidences.Should().ContainSingle().Which;
        evidence.Confidence.Should().Be(EvidenceConfidence.Probable,
            "o pipeline nunca emite Confirmed sem verificação (§19)");
        evidence.Citation.Should().Be("Knowledge#a1");
        evidence.TraceId.Should().Be("trace-9");
        evidence.Tool.Should().Be("search_knowledge");
        evidence.Source.Should().Be(EvidenceSource.Knowledge);
    }

    // ── Ingesta (§3) ───────────────────────────────────────────────────

    [Fact]
    public async Task Ingest_artigo_publicado_chunka_embute_e_faz_upsert()
    {
        var articleId = Guid.NewGuid();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(send => send.Send(It.IsAny<GetKnowledgeArticleQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeArticleDto(
                articleId, "Zabbix", "um parágrafo\n\ndois parágrafos", null, KnowledgeArticleStatus.Published));

        var store = new FakeVectorStore();
        var embedding = new FakeEmbeddingProvider();
        var service = new KnowledgeIngestionService(
            mediator.Object, embedding, store, NullLogger<KnowledgeIngestionService>.Instance);

        var indexed = await service.IngestArticleAsync(articleId);

        indexed.Should().Be(2);
        store.Documents.Should().HaveCount(2);
        store.Documents[0].Id.Should().Be($"{articleId}:0");
        store.Documents[0].Metadata["title"].Should().Be("Zabbix");
        embedding.Embedded.Should().HaveCount(2);
    }

    [Fact]
    public async Task Ingest_artigo_nao_publicado_e_recusado_ptbr()
    {
        var articleId = Guid.NewGuid();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(send => send.Send(It.IsAny<GetKnowledgeArticleQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeArticleDto(
                articleId, "Rascunho", "conteúdo", null, KnowledgeArticleStatus.Draft));

        var service = new KnowledgeIngestionService(
            mediator.Object, new FakeEmbeddingProvider(), new FakeVectorStore(),
            NullLogger<KnowledgeIngestionService>.Instance);

        var act = async () => await service.IngestArticleAsync(articleId);

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        failure.Which.Message.Should().Contain("apenas artigos publicados");
    }

    [Fact]
    public async Task Ingest_sem_vector_store_degrada_para_zero_e_nao_explode()
    {
        // ADR-038 §46: sem pgvector no ambiente o IVectorStore NÃO é
        // registrado (DI intencional) — o docstring do serviço promete que o
        // command "degrada sem store registrado". O ctor obrigatório quebrava
        // essa promessa: o host E2E (ValidateOnBuild) derrubava INTEIRO na
        // ausência do registro, e no host real a resolução estourava em tempo
        // de execução. Degradar = retornar 0 indexados com log honesto.
        var articleId = Guid.NewGuid();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(send => send.Send(It.IsAny<GetKnowledgeArticleQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeArticleDto(
                articleId, "Zabbix", "um parágrafo", null, KnowledgeArticleStatus.Published));

        var service = new KnowledgeIngestionService(
            mediator.Object, new FakeEmbeddingProvider(), null,
            NullLogger<KnowledgeIngestionService>.Instance);

        var indexed = await service.IngestArticleAsync(articleId);

        indexed.Should().Be(0, "sem vector store nada é indexado — degradação honesta, sem explosão");
    }
}

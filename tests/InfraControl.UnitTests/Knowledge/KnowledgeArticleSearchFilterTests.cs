using FluentAssertions;
using InfraControl.Application.Knowledge.Articles.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Knowledge;

/// <summary>
/// F24-T007/K7 (spec docs/32 §13) — busca avançada: filtros
/// estruturados AND (categoria, autor, tipo, tag) e termo na descrição,
/// todos pelo handler (mesmo caminho do endpoint). "Sistema"/"setor"
/// pedidos na spec não existem em nenhuma entidade — ficam documentados
/// como ausentes, sem filtro de propósito.
/// </summary>
public sealed class KnowledgeArticleSearchFilterTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public KnowledgeArticleSearchFilterTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private sealed class FakeCurrentUser(string[] roles) : ICurrentUserContext
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? UserName => "busca@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => roles;
    }

    /// <summary>Base: 2 categorias, 2 autores e 3 artigos Draft.</summary>
    private sealed record SeedBase(
        AppDbContext Context,
        Guid CategoryRedes,
        Guid AuthorOne,
        Guid AuthorTwo);

    private async Task<SeedBase> SeedAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        // FK author_id → DomainUsers (SetNull) — autores precisam existir.
        var authorOne = User.Create("um@exemplo.test", "Primeiro Autor", UserRole.Operator);
        var authorTwo = User.Create("dois@exemplo.test", "Segundo Autor", UserRole.Operator);
        context.DomainUsers.AddRange(authorOne, authorTwo);

        var redes = KnowledgeCategory.Create("Redes", null);
        var servidores = KnowledgeCategory.Create("Servidores", null);
        context.KnowledgeCategories.AddRange(redes, servidores);
        await context.SaveChangesAsync();

        context.KnowledgeArticles.AddRange(
            // A: recorte completo — categoria, tipo, tags, autor e descrição.
            KnowledgeArticle.Create(
                "Switch desconfigurado",
                "Porta GE1/0/20 sem energia.",
                redes.Id,
                type: KnowledgeArticleType.Guide,
                description: "Chave de acesso ao equipamento.",
                tags: ["switch", "vlan"],
                authorId: authorOne.Id),
            // B: outro recorte — categoria/tipo/tags/autor diferentes.
            KnowledgeArticle.Create(
                "Servidor offline",
                "Host APP-02 sem resposta.",
                servidores.Id,
                type: KnowledgeArticleType.Manual,
                tags: ["servidor"],
                authorId: authorTwo.Id),
            // C: sem categoria/tipo/tags/autor — nunca casa em filtro.
            KnowledgeArticle.Create("Artigo neutro", "Conteúdo geral.", null));
        await context.SaveChangesAsync();

        return new SeedBase(context, redes.Id, authorOne.Id, authorTwo.Id);
    }

    private static SearchKnowledgeArticlesQueryHandler SearchHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleOriginRepository(context),
            new FakeCurrentUser(["Operator"]));

    private static async Task<string[]> TitlesAsync(
        SearchKnowledgeArticlesQueryHandler handler,
        SearchKnowledgeArticlesQuery query)
        => (await handler.Handle(query, CancellationToken.None))
            .Items.Select(dto => dto.Title)
            .ToArray();

    [Fact]
    public async Task Categoria_devolve_só_os_artigos_da_categoria()
    {
        var seed = await SeedAsync();
        var handler = SearchHandler(seed.Context);

        (await TitlesAsync(handler, new SearchKnowledgeArticlesQuery(
                CategoryId: seed.CategoryRedes)))
            .Should().BeEquivalentTo("Switch desconfigurado");

        // Categoria sem artigos (e a nula do artigo C) → nada.
        (await TitlesAsync(handler, new SearchKnowledgeArticlesQuery(
                CategoryId: Guid.NewGuid())))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Autor_devolve_só_os_artigos_daquele_autor()
    {
        var seed = await SeedAsync();
        var handler = SearchHandler(seed.Context);

        (await TitlesAsync(handler, new SearchKnowledgeArticlesQuery(
                AuthorId: seed.AuthorOne)))
            .Should().BeEquivalentTo("Switch desconfigurado");

        (await TitlesAsync(handler, new SearchKnowledgeArticlesQuery(
                AuthorId: seed.AuthorTwo)))
            .Should().BeEquivalentTo("Servidor offline");

        (await TitlesAsync(handler, new SearchKnowledgeArticlesQuery(
                AuthorId: Guid.NewGuid())))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Tipo_devolve_só_aquele_tipo_e_exclui_artigos_sem_tipo()
    {
        var seed = await SeedAsync();
        var handler = SearchHandler(seed.Context);

        (await TitlesAsync(handler, new SearchKnowledgeArticlesQuery(
                Type: KnowledgeArticleType.Guide)))
            .Should().BeEquivalentTo("Switch desconfigurado");

        (await TitlesAsync(handler, new SearchKnowledgeArticlesQuery(
                Type: KnowledgeArticleType.Faq)))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Tag_casa_independente_da_caixa()
    {
        var seed = await SeedAsync();
        var handler = SearchHandler(seed.Context);

        // Gravada em lowercase pelo domínio — o filtro normaliza.
        (await TitlesAsync(handler, new SearchKnowledgeArticlesQuery(Tag: "Switch")))
            .Should().BeEquivalentTo("Switch desconfigurado");

        (await TitlesAsync(handler, new SearchKnowledgeArticlesQuery(Tag: "inexistente")))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Termo_também_casa_na_descrição()
    {
        var seed = await SeedAsync();
        var handler = SearchHandler(seed.Context);

        // Não está nem no título nem no conteúdo — só na descrição.
        (await TitlesAsync(handler, new SearchKnowledgeArticlesQuery(
                Term: "chave de acesso")))
            .Should().BeEquivalentTo("Switch desconfigurado");

        (await TitlesAsync(handler, new SearchKnowledgeArticlesQuery(Term: "zzz")))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Filtros_combinam_em_E_E_só()
    {
        var seed = await SeedAsync();
        var handler = SearchHandler(seed.Context);

        (await TitlesAsync(handler, new SearchKnowledgeArticlesQuery(
                CategoryId: seed.CategoryRedes,
                Type: KnowledgeArticleType.Manual)))
            .Should().BeEmpty();

        (await TitlesAsync(handler, new SearchKnowledgeArticlesQuery(
                CategoryId: seed.CategoryRedes,
                Type: KnowledgeArticleType.Guide,
                Tag: "VLAN")))
            .Should().BeEquivalentTo("Switch desconfigurado");

        (await TitlesAsync(handler, new SearchKnowledgeArticlesQuery(
                CategoryId: seed.CategoryRedes,
                AuthorId: seed.AuthorTwo)))
            .Should().BeEmpty();
    }
}

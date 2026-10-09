using FluentAssertions;
using InfraControl.Application.Knowledge.Articles;
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
/// F24-T003/K3 (ADR-040 §4): queries de versão — lista em ordem
/// decrescente com nomes em lote, detalhe, comparação campo a campo e
/// 404/400 pt-BR (artigo oculto para leitor, número malformado).
/// </summary>
public sealed class KnowledgeArticleVersionQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public KnowledgeArticleVersionQueryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private sealed class FakeCurrentUser(string[] roles) : ICurrentUserContext
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? UserName => "usuario@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => roles;
    }

    private static ListKnowledgeArticleVersionsQueryHandler ListHandler(
        AppDbContext context, string[] roles = ["Viewer"])
        => new(new KnowledgeArticleRepository(context), new FakeCurrentUser(roles));

    private static GetKnowledgeArticleVersionQueryHandler GetHandler(
        AppDbContext context, string[] roles = ["Viewer"])
        => new(new KnowledgeArticleRepository(context), new FakeCurrentUser(roles));

    private static CompareKnowledgeArticleVersionsQueryHandler CompareHandler(
        AppDbContext context, string[] roles = ["Viewer"])
        => new(new KnowledgeArticleRepository(context), new FakeCurrentUser(roles));

    /// <summary>Artigo publicado com v1.0 (publicação) e v1.1 (edição),
    /// autor/aprovador com nome em DomainUsers.</summary>
    private static async Task<(Guid ArticleId, Guid AuthorId)> SeedVersionedAsync(
        AppDbContext context, KnowledgeArticleStatus status = KnowledgeArticleStatus.Published)
    {
        var author = User.Create("versao.autor@exemplo.test", "Versão Autor", UserRole.Operator);
        context.DomainUsers.Add(author);
        await context.SaveChangesAsync();

        var article = KnowledgeArticle.Create("Artigo versionado", "Conteúdo v1.", null, "Guia");
        context.KnowledgeArticles.Add(article);
        await context.SaveChangesAsync();

        article.TransitionTo(KnowledgeArticleStatus.InReview);
        article.TransitionTo(KnowledgeArticleStatus.Approved);
        article.TransitionTo(KnowledgeArticleStatus.Published);
        context.KnowledgeArticleVersions.Add(KnowledgeArticleVersion.Create(
            article, 1, 0, author.Id, "Publicação inicial", approvedById: author.Id));

        article.Update("Artigo versionado", "Conteúdo v1.1.", null, "Guia",
            reason: "Primeira correção.");
        context.KnowledgeArticleVersions.Add(KnowledgeArticleVersion.Create(
            article, 1, 1, author.Id, "Primeira correção."));

        if (status != KnowledgeArticleStatus.Published)
        {
            foreach (var step in new[]
            {
                KnowledgeArticleStatus.InReview,
                KnowledgeArticleStatus.Approved,
                KnowledgeArticleStatus.Published,
            })
                article.TransitionTo(step);
            article.TransitionTo(status);
        }

        await context.SaveChangesAsync();
        return (article.Id, author.Id);
    }

    [Fact]
    public async Task ListVersions_ShouldReturnDescendingWithUserNames()
    {
        await using var context = await CreateContextAsync();
        var (articleId, authorId) = await SeedVersionedAsync(context);

        var versions = await ListHandler(context).Handle(
            new ListKnowledgeArticleVersionsQuery(articleId), CancellationToken.None);

        versions.Select(version => version.Version).Should().Equal("1.1", "1.0");
        versions[0].AuthorId.Should().Be(authorId);
        versions[0].AuthorName.Should().Be("Versão Autor");
        versions[1].ApprovedById.Should().Be(authorId);
        versions[1].ApprovedByName.Should().Be("Versão Autor");
        versions[1].Content.Should().Be("Conteúdo v1.");
    }

    [Fact]
    public async Task ListVersions_UnknownArticle_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => ListHandler(context).Handle(
            new ListKnowledgeArticleVersionsQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{unknown}' não encontrado.");
    }

    /// <summary>Leitor não vê versões de artigo em revisão (mesma regra do GET).</summary>
    [Fact]
    public async Task ListVersions_HiddenArticleAsReader_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var article = KnowledgeArticle.Create("Oculto", "Segredo.", null, "Guia");
        context.KnowledgeArticles.Add(article);
        await context.SaveChangesAsync();

        var action = () => ListHandler(context, ["Viewer"]).Handle(
            new ListKnowledgeArticleVersionsQuery(article.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{article.Id}' não encontrado.");
    }

    [Fact]
    public async Task GetVersion_ShouldReturnDetail()
    {
        await using var context = await CreateContextAsync();
        var (articleId, _) = await SeedVersionedAsync(context);

        var dto = await GetHandler(context).Handle(
            new GetKnowledgeArticleVersionQuery(articleId, 1, 1), CancellationToken.None);

        dto.Version.Should().Be("1.1");
        dto.Content.Should().Be("Conteúdo v1.1.");
        dto.Reason.Should().Be("Primeira correção.");
    }

    [Fact]
    public async Task GetVersion_UnknownVersion_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (articleId, _) = await SeedVersionedAsync(context);

        var action = () => GetHandler(context).Handle(
            new GetKnowledgeArticleVersionQuery(articleId, 2, 0), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Versão '2.0' não encontrada para o artigo '{articleId}'.");
    }

    [Fact]
    public async Task Compare_ShouldReturnOnlyChangedFields()
    {
        await using var context = await CreateContextAsync();
        var (articleId, _) = await SeedVersionedAsync(context);

        var compare = await CompareHandler(context).Handle(
            new CompareKnowledgeArticleVersionsQuery(articleId, "1.0", "1.1"),
            CancellationToken.None);

        compare.From.Should().Be("1.0");
        compare.To.Should().Be("1.1");
        compare.Reason.Should().Be("Primeira correção.");
        compare.Changes.Should().ContainSingle(diff => diff.Field == "content")
            .Which.Should().Be(
                new KnowledgeArticleVersionDiff("content", "Conteúdo v1.", "Conteúdo v1.1."));
    }

    [Fact]
    public async Task Compare_SameVersion_ShouldReturnEmptyChanges()
    {
        await using var context = await CreateContextAsync();
        var (articleId, _) = await SeedVersionedAsync(context);

        var compare = await CompareHandler(context).Handle(
            new CompareKnowledgeArticleVersionsQuery(articleId, "1.1", "1.1"),
            CancellationToken.None);

        compare.Changes.Should().BeEmpty();
    }

    [Fact]
    public async Task Compare_InvalidFormat_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var (articleId, _) = await SeedVersionedAsync(context);

        var action = () => CompareHandler(context).Handle(
            new CompareKnowledgeArticleVersionsQuery(articleId, "x", "1.0"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Número de versão inválido: 'x'. Use o formato 'major.minor'*");
    }

    [Fact]
    public async Task Compare_UnknownVersion_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (articleId, _) = await SeedVersionedAsync(context);

        var action = () => CompareHandler(context).Handle(
            new CompareKnowledgeArticleVersionsQuery(articleId, "1.0", "9.9"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Versão '9.9' não encontrada para o artigo '{articleId}'.");
    }
}

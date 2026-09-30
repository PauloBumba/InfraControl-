using FluentAssertions;
using InfraControl.Application.Knowledge.Articles.Commands;
using InfraControl.Application.Knowledge.Articles.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Knowledge;

/// <summary>
/// F10-T003: Publishing — transições do ciclo Draft→Published→Archived→Draft
/// (409 pt-BR fora do ciclo), status fora do enum (400 pt-BR) e rascunhos
/// invisíveis para quem não tem nível de escrita (docs/04).
/// </summary>
public sealed class KnowledgeArticleStatusCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public KnowledgeArticleStatusCommandTests()
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

    private static ChangeKnowledgeArticleStatusCommandHandler ChangeStatusHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            NullLogger<ChangeKnowledgeArticleStatusCommandHandler>.Instance);

    private static SearchKnowledgeArticlesQueryHandler SearchHandler(AppDbContext context, string[] roles)
        => new(new KnowledgeArticleRepository(context), new FakeCurrentUser(roles));

    private static GetKnowledgeArticleQueryHandler GetHandler(AppDbContext context, string[] roles)
        => new(new KnowledgeArticleRepository(context), new FakeCurrentUser(roles));

    private static async Task<Guid> SeedArticleAsync(
        AppDbContext context,
        KnowledgeArticleStatus status = KnowledgeArticleStatus.Draft)
    {
        var article = KnowledgeArticle.Create("Artigo de publicação", "Conteúdo.", null);
        context.KnowledgeArticles.Add(article);
        await context.SaveChangesAsync();

        if (status == KnowledgeArticleStatus.Published)
            article.TransitionTo(KnowledgeArticleStatus.Published);
        else if (status == KnowledgeArticleStatus.Archived)
        {
            article.TransitionTo(KnowledgeArticleStatus.Published);
            article.TransitionTo(KnowledgeArticleStatus.Archived);
        }

        await context.SaveChangesAsync();
        return article.Id;
    }

    [Fact]
    public async Task ChangeStatus_DraftToPublished_ShouldPersistAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

        var dto = await ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "published"),
            CancellationToken.None);

        dto.Status.Should().Be(KnowledgeArticleStatus.Published);
        var saved = await context.KnowledgeArticles.AsNoTracking()
            .SingleAsync(article => article.Id == articleId);
        saved.Status.Should().Be(KnowledgeArticleStatus.Published);
    }

    /// <summary>Ciclo completo via handler: Draft → Published → Archived → Draft.</summary>
    [Fact]
    public async Task ChangeStatus_FullCycle_ShouldPersistEachStep()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

        var published = await ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "Published"),
            CancellationToken.None);
        published.Status.Should().Be(KnowledgeArticleStatus.Published);

        var archived = await ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "archived"),
            CancellationToken.None);
        archived.Status.Should().Be(KnowledgeArticleStatus.Archived);

        var restored = await ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "draft"),
            CancellationToken.None);
        restored.Status.Should().Be(KnowledgeArticleStatus.Draft);

        var saved = await context.KnowledgeArticles.AsNoTracking()
            .SingleAsync(article => article.Id == articleId);
        saved.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    [Fact]
    public async Task ChangeStatus_InvalidTransition_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

        var action = () => ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "archived"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Transição inválida: 'Draft' → 'Archived'.");
    }

    [Fact]
    public async Task ChangeStatus_SameState_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context, KnowledgeArticleStatus.Published);

        var action = () => ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "published"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Transição inválida: 'Published' → 'Published'.");
    }

    [Fact]
    public async Task ChangeStatus_UnknownArticle_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(unknown, "published"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task ChangeStatus_InvalidStatusString_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

        var action = () => ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "promoted"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Status inválido: 'promoted'. Use 'draft', 'published' ou 'archived'.*");
    }

    [Fact]
    public async Task Search_WithoutOperateRole_ShouldHideDrafts()
    {
        await using var context = await CreateContextAsync();
        await SeedArticleAsync(context); // Draft
        await SeedArticleAsync(context, KnowledgeArticleStatus.Published);

        var page = await SearchHandler(context, ["Viewer"]).Handle(
            new SearchKnowledgeArticlesQuery(), CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle(article =>
            article.Status == KnowledgeArticleStatus.Published);
    }

    [Fact]
    public async Task Search_AsOperator_ShouldIncludeDrafts()
    {
        await using var context = await CreateContextAsync();
        await SeedArticleAsync(context); // Draft
        await SeedArticleAsync(context, KnowledgeArticleStatus.Published);

        var page = await SearchHandler(context, ["Operator"]).Handle(
            new SearchKnowledgeArticlesQuery(), CancellationToken.None);

        page.TotalCount.Should().Be(2);
    }

    /// <summary>Rascunho sem escrita responde 404 — não vaza a existência.</summary>
    [Fact]
    public async Task Get_DraftAsReader_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

        var action = () => GetHandler(context, ["Viewer"]).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{articleId}' não encontrado.");
    }

    [Fact]
    public async Task Get_DraftAsOperator_ShouldReturnArticle()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

        var dto = await GetHandler(context, ["Operator"]).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);

        dto.Id.Should().Be(articleId);
        dto.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    [Fact]
    public async Task Get_PublishedAsReader_ShouldReturnArticle()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context, KnowledgeArticleStatus.Published);

        var dto = await GetHandler(context, ["Viewer"]).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);

        dto.Id.Should().Be(articleId);
        dto.Status.Should().Be(KnowledgeArticleStatus.Published);
    }
}

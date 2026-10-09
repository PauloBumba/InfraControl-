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
/// F24-T002 (K2): workflow de 8 estados (ADR-039, docs/32 §7) — matriz de
/// transições com 409 pt-BR em rótulos, status fora do enum = 400 com as
/// 8 opções e estados não publicados invisíveis para quem não escreve.
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

    private sealed class FakeCurrentUser(string[] roles, Guid? userId = null) : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId ?? Guid.NewGuid();
        public string? UserName => "usuario@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => roles;
    }

    private static ChangeKnowledgeArticleStatusCommandHandler ChangeStatusHandler(
        AppDbContext context, ICurrentUserContext? currentUser = null)
        => new(
            new KnowledgeArticleRepository(context),
            currentUser ?? new FakeCurrentUser(["Operator"]),
            NullLogger<ChangeKnowledgeArticleStatusCommandHandler>.Instance);

    private static SearchKnowledgeArticlesQueryHandler SearchHandler(AppDbContext context, string[] roles)
        => new(new KnowledgeArticleRepository(context), new FakeCurrentUser(roles));

    private static GetKnowledgeArticleQueryHandler GetHandler(AppDbContext context, string[] roles)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleDeviceRepository(context),
            new KnowledgeArticleIncidentRepository(context),
            new KnowledgeArticleServiceRepository(context),
            new FakeCurrentUser(roles));

    private static IReadOnlyList<KnowledgeArticleStatus> PathTo(KnowledgeArticleStatus target)
        => target switch
        {
            KnowledgeArticleStatus.InReview => [KnowledgeArticleStatus.InReview],
            KnowledgeArticleStatus.Approved
                => [KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Approved],
            KnowledgeArticleStatus.Published
                => [KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Approved,
                    KnowledgeArticleStatus.Published],
            KnowledgeArticleStatus.Updated
                => [KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Approved,
                    KnowledgeArticleStatus.Published, KnowledgeArticleStatus.InReview,
                    KnowledgeArticleStatus.Updated],
            KnowledgeArticleStatus.Archived
                => [KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Approved,
                    KnowledgeArticleStatus.Published, KnowledgeArticleStatus.Archived],
            KnowledgeArticleStatus.Obsolete
                => [KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Approved,
                    KnowledgeArticleStatus.Published, KnowledgeArticleStatus.Obsolete],
            KnowledgeArticleStatus.Rejected
                => [KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Rejected],
            _ => [],
        };

    private static async Task<Guid> SeedArticleAsync(
        AppDbContext context,
        KnowledgeArticleStatus status = KnowledgeArticleStatus.Draft)
    {
        var article = KnowledgeArticle.Create("Artigo de publicação", "Conteúdo.", null, "Manual");
        context.KnowledgeArticles.Add(article);
        await context.SaveChangesAsync();

        foreach (var step in PathTo(status))
            article.TransitionTo(step);

        if (status != KnowledgeArticleStatus.Draft)
            await context.SaveChangesAsync();

        return article.Id;
    }

    [Fact]
    public async Task ChangeStatus_DraftToInReview_ShouldPersistAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

        var dto = await ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "inReview"),
            CancellationToken.None);

        dto.Status.Should().Be(KnowledgeArticleStatus.InReview);
        var saved = await context.KnowledgeArticles.AsNoTracking()
            .SingleAsync(article => article.Id == articleId);
        saved.Status.Should().Be(KnowledgeArticleStatus.InReview);
    }

    /// <summary>Ciclo completo do §7 via handler: RASCUNHO → EM REVISÃO →
    /// APROVADA → PUBLICADA → EM REVISÃO → ATUALIZADA.</summary>
    [Fact]
    public async Task ChangeStatus_FullEditorialFlow_ShouldPersistEachStep()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

        foreach (var (input, expected) in new (string, KnowledgeArticleStatus)[]
        {
            ("inReview", KnowledgeArticleStatus.InReview),
            ("approved", KnowledgeArticleStatus.Approved),
            ("published", KnowledgeArticleStatus.Published),
            ("inReview", KnowledgeArticleStatus.InReview),
            ("updated", KnowledgeArticleStatus.Updated),
        })
        {
            var dto = await ChangeStatusHandler(context).Handle(
                new ChangeKnowledgeArticleStatusCommand(articleId, input),
                CancellationToken.None);
            dto.Status.Should().Be(expected);
        }

        var saved = await context.KnowledgeArticles.AsNoTracking()
            .SingleAsync(article => article.Id == articleId);
        saved.Status.Should().Be(KnowledgeArticleStatus.Updated);
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
            .WithMessage("Transição inválida: 'Rascunho' → 'Arquivada'.");
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
            .WithMessage("Transição inválida: 'Publicada' → 'Publicada'.");
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
            .WithMessage(
                "Status inválido: 'promoted'. Use 'draft', 'inReview', 'approved', " +
                "'published', 'updated', 'archived', 'obsolete' ou 'rejected'.*");
    }

    /// <summary>"99" parseia como enum fora do domínio — tem de ser 400.</summary>
    [Fact]
    public async Task ChangeStatus_NumericStatusString_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

        var action = () => ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "99"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Status inválido: '99'.*");
    }

    /// <summary>K3: a 1ª publicação cria o snapshot (1,0) com aprovador.</summary>
    [Fact]
    public async Task ChangeStatus_FirstPublish_ShouldCreateVersion10()
    {
        await using var context = await CreateContextAsync();
        var approverId = Guid.NewGuid();
        var articleId = await SeedArticleAsync(context);
        var handler = ChangeStatusHandler(
            context, new FakeCurrentUser(["Operator"], approverId));

        await handler.Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "inReview"),
            CancellationToken.None);
        await handler.Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "approved"),
            CancellationToken.None);
        await handler.Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "published"),
            CancellationToken.None);

        var version = await context.KnowledgeArticleVersions.SingleAsync();
        version.Major.Should().Be(1);
        version.Minor.Should().Be(0);
        version.ApprovedById.Should().Be(approverId);
        version.AuthorId.Should().Be(approverId);
        version.Reason.Should().Be("Publicação inicial");
        version.Status.Should().Be(KnowledgeArticleStatus.Published);
    }

    /// <summary>K3: re-publicação após revisão = major+1 (o v2.0 do §8).</summary>
    [Fact]
    public async Task ChangeStatus_Republish_ShouldCreateNextMajor()
    {
        await using var context = await CreateContextAsync();
        var approverId = Guid.NewGuid();
        var articleId = await SeedArticleAsync(context, KnowledgeArticleStatus.Published);
        var handler = ChangeStatusHandler(
            context, new FakeCurrentUser(["Operator"], approverId));

        await handler.Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "inReview"),
            CancellationToken.None);
        await handler.Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "approved"),
            CancellationToken.None);
        await handler.Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "published"),
            CancellationToken.None);

        var version = await context.KnowledgeArticleVersions.SingleAsync();
        version.Major.Should().Be(2);
        version.Minor.Should().Be(0);
        version.ApprovedById.Should().Be(approverId);
        version.Reason.Should().Be("Publicação após revisão");
    }

    [Fact]
    public async Task ChangeStatus_NonPublishTransition_ShouldNotCreateVersion()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

        await ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "inReview"),
            CancellationToken.None);

        (await context.KnowledgeArticleVersions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Search_WithoutOperateRole_ShouldHideUnpublished()
    {
        await using var context = await CreateContextAsync();
        await SeedArticleAsync(context); // Draft
        await SeedArticleAsync(context, KnowledgeArticleStatus.InReview);
        await SeedArticleAsync(context, KnowledgeArticleStatus.Rejected);
        await SeedArticleAsync(context, KnowledgeArticleStatus.Published);

        var page = await SearchHandler(context, ["Viewer"]).Handle(
            new SearchKnowledgeArticlesQuery(), CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle(article =>
            article.Status == KnowledgeArticleStatus.Published);
    }

    [Fact]
    public async Task Search_AsOperator_ShouldIncludeAllStates()
    {
        await using var context = await CreateContextAsync();
        await SeedArticleAsync(context); // Draft
        await SeedArticleAsync(context, KnowledgeArticleStatus.InReview);
        await SeedArticleAsync(context, KnowledgeArticleStatus.Rejected);
        await SeedArticleAsync(context, KnowledgeArticleStatus.Published);

        var page = await SearchHandler(context, ["Operator"]).Handle(
            new SearchKnowledgeArticlesQuery(), CancellationToken.None);

        page.TotalCount.Should().Be(4);
    }

    /// <summary>Artigo em revisão sem escrita responde 404 — não vaza a existência.</summary>
    [Fact]
    public async Task Get_UnpublishedAsReader_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context, KnowledgeArticleStatus.InReview);

        var action = () => GetHandler(context, ["Viewer"]).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{articleId}' não encontrado.");
    }

    [Fact]
    public async Task Get_InReviewAsOperator_ShouldReturnArticle()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context, KnowledgeArticleStatus.InReview);

        var dto = await GetHandler(context, ["Operator"]).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);

        dto.Id.Should().Be(articleId);
        dto.Status.Should().Be(KnowledgeArticleStatus.InReview);
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

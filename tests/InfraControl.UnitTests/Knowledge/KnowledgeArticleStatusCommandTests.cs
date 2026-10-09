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
<<<<<<< HEAD
/// F24-T002/K2: workflow de revisão (ADR-034, spec docs/32 §7) —
/// cadeia de aprovação e revisão do publicado (409 pt-BR fora da
/// matriz), status fora do enum (400 pt-BR com as 8 opções) e
/// nunca-publicados invisíveis para quem não escreve (docs/04).
/// Herda do F10-T003 a rota, a policy e os 404 de não vazamento.
=======
/// F24-T002 (K2): workflow de 8 estados (ADR-039, docs/32 §7) — matriz de
/// transições com 409 pt-BR em rótulos, status fora do enum = 400 com as
/// 8 opções e estados não publicados invisíveis para quem não escreve.
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
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
            new KnowledgeArticleVersionRepository(context),
            new FakeCurrentUser(["Operator"]),
            NullLogger<ChangeKnowledgeArticleStatusCommandHandler>.Instance);

    private static SearchKnowledgeArticlesQueryHandler SearchHandler(AppDbContext context, string[] roles)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleOriginRepository(context),
            new FakeCurrentUser(roles));

    private static GetKnowledgeArticleQueryHandler GetHandler(AppDbContext context, string[] roles)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleDeviceRepository(context),
            new KnowledgeArticleIncidentRepository(context),
            new KnowledgeArticleServiceRepository(context),
            new KnowledgeArticleOriginRepository(context),
            new FakeCurrentUser(roles));

<<<<<<< HEAD
    /// <summary>
    /// Caminho válido do domínio até o estado pedido (ADR-034 §3) — o
    /// seed nunca "pula" a matriz.
    /// </summary>
    private static KnowledgeArticleStatus[] PathTo(KnowledgeArticleStatus status)
        => status switch
        {
            KnowledgeArticleStatus.Draft => [],
            KnowledgeArticleStatus.InReview => [KnowledgeArticleStatus.InReview],
            KnowledgeArticleStatus.Approved =>
                [KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Approved],
            KnowledgeArticleStatus.Published =>
                [KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Approved,
                 KnowledgeArticleStatus.Published],
            KnowledgeArticleStatus.Updated =>
                [KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Approved,
                 KnowledgeArticleStatus.Published, KnowledgeArticleStatus.InReview,
                 KnowledgeArticleStatus.Updated],
            KnowledgeArticleStatus.Archived =>
                [KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Approved,
                 KnowledgeArticleStatus.Published, KnowledgeArticleStatus.Archived],
            KnowledgeArticleStatus.Obsolete =>
                [KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Approved,
                 KnowledgeArticleStatus.Published, KnowledgeArticleStatus.Obsolete],
            KnowledgeArticleStatus.Rejected =>
                [KnowledgeArticleStatus.InReview, KnowledgeArticleStatus.Rejected],
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
=======
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
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
        };

    private static async Task<Guid> SeedArticleAsync(
        AppDbContext context,
        KnowledgeArticleStatus status = KnowledgeArticleStatus.Draft,
        string title = "Artigo de publicação")
    {
<<<<<<< HEAD
        var article = KnowledgeArticle.Create(title, "Conteúdo.", null);
=======
        var article = KnowledgeArticle.Create("Artigo de publicação", "Conteúdo.", null, "Manual");
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
        context.KnowledgeArticles.Add(article);
        await context.SaveChangesAsync();

        foreach (var step in PathTo(status))
<<<<<<< HEAD
        {
            article.TransitionTo(step);
            await context.SaveChangesAsync();
        }
=======
            article.TransitionTo(step);

        if (status != KnowledgeArticleStatus.Draft)
            await context.SaveChangesAsync();
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c

        return article.Id;
    }

    [Fact]
<<<<<<< HEAD
    public async Task ChangeStatus_ApprovalChain_ShouldPersistEachStep()
=======
    public async Task ChangeStatus_DraftToInReview_ShouldPersistAndReturnDto()
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

<<<<<<< HEAD
        var inReview = await ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "inreview"),
            CancellationToken.None);
        inReview.Status.Should().Be(KnowledgeArticleStatus.InReview);

        var approved = await ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "APPROVED"),
            CancellationToken.None);
        approved.Status.Should().Be(KnowledgeArticleStatus.Approved);

        var published = await ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "published"),
=======
        var dto = await ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "inReview"),
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
            CancellationToken.None);
        published.Status.Should().Be(KnowledgeArticleStatus.Published);

<<<<<<< HEAD
=======
        dto.Status.Should().Be(KnowledgeArticleStatus.InReview);
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
        var saved = await context.KnowledgeArticles.AsNoTracking()
            .SingleAsync(article => article.Id == articleId);
        saved.Status.Should().Be(KnowledgeArticleStatus.InReview);
    }

<<<<<<< HEAD
    /// <summary>Revisão do publicado fecha em Atualizada (§7).</summary>
    [Fact]
    public async Task ChangeStatus_PublishedRevision_ShouldReachUpdated()
=======
    /// <summary>Ciclo completo do §7 via handler: RASCUNHO → EM REVISÃO →
    /// APROVADA → PUBLICADA → EM REVISÃO → ATUALIZADA.</summary>
    [Fact]
    public async Task ChangeStatus_FullEditorialFlow_ShouldPersistEachStep()
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context, KnowledgeArticleStatus.Published);

<<<<<<< HEAD
        var inReview = await ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "inreview"),
            CancellationToken.None);
        inReview.Status.Should().Be(KnowledgeArticleStatus.InReview);

        var updated = await ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "updated"),
            CancellationToken.None);
        updated.Status.Should().Be(KnowledgeArticleStatus.Updated);
=======
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
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c

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
<<<<<<< HEAD
            .WithMessage("Status inválido: 'promoted'. Use 'draft', 'inreview', 'approved', 'published', 'updated', 'archived', 'obsolete' ou 'rejected'.*");
=======
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
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
    }

    /// <summary>ADR-034 §4: leitor vê só o que já foi publicado.</summary>
    [Fact]
    public async Task Search_WithoutOperateRole_ShouldHideUnpublished()
    {
        await using var context = await CreateContextAsync();
<<<<<<< HEAD
        await SeedArticleAsync(context, KnowledgeArticleStatus.Draft, "rascunho");
        await SeedArticleAsync(context, KnowledgeArticleStatus.InReview, "em revisão");
        await SeedArticleAsync(context, KnowledgeArticleStatus.Approved, "aprovada");
        await SeedArticleAsync(context, KnowledgeArticleStatus.Rejected, "rejeitada");
        await SeedArticleAsync(context, KnowledgeArticleStatus.Published, "publicada");
        await SeedArticleAsync(context, KnowledgeArticleStatus.Updated, "atualizada");
        await SeedArticleAsync(context, KnowledgeArticleStatus.Archived, "arquivada");
        await SeedArticleAsync(context, KnowledgeArticleStatus.Obsolete, "obsoleta");
=======
        await SeedArticleAsync(context); // Draft
        await SeedArticleAsync(context, KnowledgeArticleStatus.InReview);
        await SeedArticleAsync(context, KnowledgeArticleStatus.Rejected);
        await SeedArticleAsync(context, KnowledgeArticleStatus.Published);
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c

        var page = await SearchHandler(context, ["Viewer"]).Handle(
            new SearchKnowledgeArticlesQuery(), CancellationToken.None);

        page.TotalCount.Should().Be(4);
        page.Items.Should().OnlyContain(article =>
            article.Status == KnowledgeArticleStatus.Published
            || article.Status == KnowledgeArticleStatus.Updated
            || article.Status == KnowledgeArticleStatus.Archived
            || article.Status == KnowledgeArticleStatus.Obsolete);
    }

    [Fact]
<<<<<<< HEAD
    public async Task Search_AsOperator_ShouldIncludeUnpublished()
    {
        await using var context = await CreateContextAsync();
        await SeedArticleAsync(context, KnowledgeArticleStatus.Draft, "rascunho");
        await SeedArticleAsync(context, KnowledgeArticleStatus.InReview, "em revisão");
        await SeedArticleAsync(context, KnowledgeArticleStatus.Rejected, "rejeitada");
        await SeedArticleAsync(context, KnowledgeArticleStatus.Published, "publicada");
=======
    public async Task Search_AsOperator_ShouldIncludeAllStates()
    {
        await using var context = await CreateContextAsync();
        await SeedArticleAsync(context); // Draft
        await SeedArticleAsync(context, KnowledgeArticleStatus.InReview);
        await SeedArticleAsync(context, KnowledgeArticleStatus.Rejected);
        await SeedArticleAsync(context, KnowledgeArticleStatus.Published);
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c

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

    /// <summary>Generalização da regra: Em revisão também não vaza.</summary>
    [Fact]
    public async Task Get_InReviewAsReader_ShouldThrowNotFoundPtBr()
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

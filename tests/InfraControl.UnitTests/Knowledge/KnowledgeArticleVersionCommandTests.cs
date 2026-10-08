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
/// F24-T003/K3: histórico de versões nunca perdidas (spec docs/32 §8).
/// Criação semeia a v1.0, edição abre minor com motivo obrigatório,
/// aprovação (K2) carimba o aprovador e põe a próxima edição em major
/// novo; listagem/comparação com a mesma visibilidade do artigo.
/// </summary>
public sealed class KnowledgeArticleVersionCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public KnowledgeArticleVersionCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    // Autor e aprovador da mesma linha — a FK author_id/approved_by_id
    // → DomainUsers é SetNull, o usuário precisa existir.
    private static readonly Guid CurrentUserId = Guid.NewGuid();

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        if (!await context.DomainUsers.AnyAsync(user => user.Id == CurrentUserId))
        {
            context.DomainUsers.Add(User.Create(
                CurrentUserId, "autor@exemplo.test", "Autor do Teste",
                UserRole.Operator));
            await context.SaveChangesAsync();
        }
        return context;
    }

    private sealed class FakeCurrentUser(string[] roles) : ICurrentUserContext
    {
        public Guid? UserId { get; } = CurrentUserId;
        public string? UserName => "autor@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => roles;
    }

    private static CreateKnowledgeArticleCommandHandler CreateHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeCategoryRepository(context),
            new KnowledgeArticleVersionRepository(context),
            new FakeCurrentUser(["Operator"]),
            NullLogger<CreateKnowledgeArticleCommandHandler>.Instance);

    private static UpdateKnowledgeArticleCommandHandler UpdateHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeCategoryRepository(context),
            new KnowledgeArticleVersionRepository(context),
            new FakeCurrentUser(["Operator"]),
            NullLogger<UpdateKnowledgeArticleCommandHandler>.Instance);

    private static ChangeKnowledgeArticleStatusCommandHandler ChangeStatusHandler(
        AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleVersionRepository(context),
            new FakeCurrentUser(["Operator"]),
            NullLogger<ChangeKnowledgeArticleStatusCommandHandler>.Instance);

    private static ListKnowledgeArticleVersionsQueryHandler ListHandler(
        AppDbContext context, string[] roles)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleVersionRepository(context),
            new FakeCurrentUser(roles));

    private static CompareKnowledgeArticleVersionsQueryHandler CompareHandler(
        AppDbContext context, string[] roles)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleVersionRepository(context),
            new FakeCurrentUser(roles));

    private static async Task<Guid> SeedArticleAsync(AppDbContext context)
    {
        var dto = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Roteiro", "Conteúdo original.", null),
            CancellationToken.None);
        return dto.Id;
    }

    [Fact]
    public async Task Create_ShouldSeedVersion10_WithAuthorAndCurrent()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

        var versions = await ListHandler(context, ["Operator"]).Handle(
            new ListKnowledgeArticleVersionsQuery(articleId), CancellationToken.None);

        versions.Should().ContainSingle();
        var only = versions.Single();
        only.Version.Should().Be("1.0");
        only.Reason.Should().Be("Criação do artigo");
        only.AuthorId.Should().Be(CurrentUserId);
        only.AuthorName.Should().Be("Autor do Teste");
        only.IsCurrent.Should().BeTrue();
        only.ApprovedById.Should().BeNull();
    }

    [Fact]
    public async Task Update_ShouldOpenMinorVersion_WithReason()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

        await UpdateHandler(context).Handle(
            new UpdateKnowledgeArticleCommand(
                articleId, "Roteiro", "Conteúdo novo.", null,
                "Rotina passou a ser diária."),
            CancellationToken.None);

        var article = await context.KnowledgeArticles.AsNoTracking()
            .SingleAsync(row => row.Id == articleId);
        article.Version.Should().Be("1.1");

        var versions = await ListHandler(context, ["Operator"]).Handle(
            new ListKnowledgeArticleVersionsQuery(articleId), CancellationToken.None);

        // Mais recente primeiro (1.1, depois 1.0) — só a corrente é atual.
        var ordered = versions.ToArray();
        ordered.Select(row => row.Version).Should().Equal("1.1", "1.0");
        ordered[0].Reason.Should().Be("Rotina passou a ser diária.");
        ordered[0].IsCurrent.Should().BeTrue();
        ordered[1].IsCurrent.Should().BeFalse();
    }

    [Fact]
    public async Task Update_BlankReason_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

        var action = () => UpdateHandler(context).Handle(
            new UpdateKnowledgeArticleCommand(
                articleId, "Roteiro", "Conteúdo novo.", null, "   "),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O motivo da alteração é obrigatório.*");

        // Nada gravado: a versão corrente segue 1.0.
        var article = await context.KnowledgeArticles.AsNoTracking()
            .SingleAsync(row => row.Id == articleId);
        article.Version.Should().Be("1.0");
    }

    /// <summary>Aprovação carimba o aprovador e a próxima edição abre
    /// o major novo (§8: v1.1 → v2.0).</summary>
    [Fact]
    public async Task ChangeStatus_Approved_ShouldStampApproverAndOpenMajorNextEdit()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);
        await UpdateHandler(context).Handle(
            new UpdateKnowledgeArticleCommand(
                articleId, "Roteiro", "Conteúdo revisado.", null,
                "Revisão de rotina."),
            CancellationToken.None);

        await ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "inreview"),
            CancellationToken.None);
        var approved = await ChangeStatusHandler(context).Handle(
            new ChangeKnowledgeArticleStatusCommand(articleId, "approved"),
            CancellationToken.None);
        approved.Version.Should().Be("1.1");

        var versions = await ListHandler(context, ["Operator"]).Handle(
            new ListKnowledgeArticleVersionsQuery(articleId), CancellationToken.None);
        var stamped = versions.Single(row => row.Version == "1.1");
        stamped.ApprovedById.Should().Be(CurrentUserId);
        stamped.ApprovedByName.Should().Be("Autor do Teste");
        stamped.ApprovedAt.Should().NotBeNull();

        await UpdateHandler(context).Handle(
            new UpdateKnowledgeArticleCommand(
                articleId, "Roteiro", "Pós-aprovação.", null,
                "Ajuste pós-aprovação."),
            CancellationToken.None);

        var article = await context.KnowledgeArticles.AsNoTracking()
            .SingleAsync(row => row.Id == articleId);
        article.Version.Should().Be("2.0");
    }

    [Fact]
    public async Task ListVersions_UnknownArticle_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => ListHandler(context, ["Operator"]).Handle(
            new ListKnowledgeArticleVersionsQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{unknown}' não encontrado.");
    }

    /// <summary>Mesma visibilidade do artigo (ADR-034 §4): estado
    /// nunca-publicado sem escrita = 404, não vaza existência.</summary>
    [Fact]
    public async Task ListVersions_DraftAsReader_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

        var action = () => ListHandler(context, ["Viewer"]).Handle(
            new ListKnowledgeArticleVersionsQuery(articleId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{articleId}' não encontrado.");
    }

    [Fact]
    public async Task Compare_ShouldReturnBothSnapshots()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);
        await UpdateHandler(context).Handle(
            new UpdateKnowledgeArticleCommand(
                articleId, "Roteiro v2", "Conteúdo novo.", null,
                "Reescrita completa."),
            CancellationToken.None);

        var compare = await CompareHandler(context, ["Operator"]).Handle(
            new CompareKnowledgeArticleVersionsQuery(articleId, "1.0", "1.1"),
            CancellationToken.None);

        compare.From.Version.Should().Be("1.0");
        compare.From.Title.Should().Be("Roteiro");
        compare.From.Content.Should().Be("Conteúdo original.");
        compare.From.Reason.Should().Be("Criação do artigo");

        compare.To.Version.Should().Be("1.1");
        compare.To.Title.Should().Be("Roteiro v2");
        compare.To.Content.Should().Be("Conteúdo novo.");
        compare.To.Reason.Should().Be("Reescrita completa.");
        compare.To.AuthorName.Should().Be("Autor do Teste");
    }

    [Fact]
    public async Task Compare_UnknownVersion_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

        var action = () => CompareHandler(context, ["Operator"]).Handle(
            new CompareKnowledgeArticleVersionsQuery(articleId, "9.9", "1.0"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("Versão '9.9' não encontrada.");
    }
}

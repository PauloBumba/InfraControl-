using FluentAssertions;
using InfraControl.Application.Knowledge.Articles;
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
/// F10-T002: handlers de KnowledgeArticle — CRUD, 404 (artigo e
/// categoria de destino), status Draft no create e busca.
/// </summary>
public sealed class KnowledgeArticleCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public KnowledgeArticleCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    // F24-T001/K1: o autor do create vem do ICurrentUserContext e a FK
    // author_id → DomainUsers é SetNull — o usuário precisa existir.
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

    private static async Task<Guid> SeedUserAsync(
        AppDbContext context, string email, string displayName)
    {
        var user = User.Create(email, displayName, UserRole.Operator);
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    private static CreateKnowledgeArticleCommandHandler CreateHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeCategoryRepository(context),
            new KnowledgeArticleVersionRepository(context),
            new FakeCurrentUser(["Operator"], CurrentUserId),
            NullLogger<CreateKnowledgeArticleCommandHandler>.Instance);

    private static UpdateKnowledgeArticleCommandHandler UpdateHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeCategoryRepository(context),
            new KnowledgeArticleVersionRepository(context),
            new FakeCurrentUser(["Operator"], CurrentUserId),
            NullLogger<UpdateKnowledgeArticleCommandHandler>.Instance);

    private static DeleteKnowledgeArticleCommandHandler DeleteHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            NullLogger<DeleteKnowledgeArticleCommandHandler>.Instance);

    private sealed class FakeCurrentUser(string[] roles, Guid? userId = null)
        : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId ?? Guid.NewGuid();
        public string? UserName => "usuario@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => roles;
    }

    private static SearchKnowledgeArticlesQueryHandler SearchHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new FakeCurrentUser(["Operator"]));

    private static GetKnowledgeArticleQueryHandler GetHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleDeviceRepository(context),
            new KnowledgeArticleIncidentRepository(context),
            new KnowledgeArticleServiceRepository(context),
            new FakeCurrentUser(["Operator"]));

    private static async Task<Guid> SeedCategoryAsync(AppDbContext context, string name = "Redes")
    {
        var category = KnowledgeCategory.Create(name, null);
        context.KnowledgeCategories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    [Fact]
    public async Task Create_ShouldPersistAndReturnDraftDto()
    {
        await using var context = await CreateContextAsync();
        var categoryId = await SeedCategoryAsync(context);

        var dto = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand(
                "Troca do switch central", "Failover passo a passo.", categoryId),
            CancellationToken.None);

        dto.Title.Should().Be("Troca do switch central");
        dto.CategoryId.Should().Be(categoryId);
        dto.Status.Should().Be(KnowledgeArticleStatus.Draft);

        var saved = await context.KnowledgeArticles.AsNoTracking()
            .SingleAsync(article => article.Id == dto.Id);
        saved.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    [Fact]
    public async Task Create_UnknownCategory_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Artigo", "Conteúdo.", unknown),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Categoria '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Create_EmptyTitle_ShouldThrowPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("  ", "Conteúdo.", null),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O título do artigo é obrigatório.*");
    }

    [Fact]
    public async Task Update_ShouldPersistChanges_KeepingStatus()
    {
        await using var context = await CreateContextAsync();
        var categoryId = await SeedCategoryAsync(context, "Backup");
        var created = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Roteiro de backup", "Conteúdo.", null),
            CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateKnowledgeArticleCommand(
                created.Id, "Roteiro de backup diário", "Conteúdo novo.", categoryId,
                "Rotina semanal passou a ser diária."),
            CancellationToken.None);

        var updated = await context.KnowledgeArticles.AsNoTracking()
            .SingleAsync(article => article.Id == created.Id);
        updated.Title.Should().Be("Roteiro de backup diário");
        updated.Content.Should().Be("Conteúdo novo.");
        updated.CategoryId.Should().Be(categoryId);
        updated.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    [Fact]
    public async Task Update_UnknownArticle_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(
            new UpdateKnowledgeArticleCommand(
                unknown, "Título", "Conteúdo.", null, "Motivo."),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Update_UnknownCategory_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Artigo", "Conteúdo.", null),
            CancellationToken.None);
        var unknown = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(
            new UpdateKnowledgeArticleCommand(
                created.Id, "Artigo", "Conteúdo.", unknown, "Motivo."),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Categoria '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Delete_UnknownArticle_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => DeleteHandler(context).Handle(
            new DeleteKnowledgeArticleCommand(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Search_WithTerm_ShouldMatchTitle()
    {
        await using var context = await CreateContextAsync();
        await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Troca do switch central", "Conteúdo A.", null),
            CancellationToken.None);
        var other = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Roteiro de backup", "Conteúdo B.", null),
            CancellationToken.None);

        var page = await SearchHandler(context).Handle(
            new SearchKnowledgeArticlesQuery("switch"), CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.Items.Should().NotContain(article => article.Id == other.Id);
        page.Items.Should().ContainSingle(article => article.Title == "Troca do switch central");
    }

    [Fact]
    public async Task Get_UnknownArticle_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{unknown}' não encontrado.");
    }

    // ── F24-T001/K1 — identificação (docs/32 §5–§6) ─────────────────────

    [Fact]
    public async Task Create_ShouldPersistIdentification()
    {
        await using var context = await CreateContextAsync();
        var responsibleId = await SeedUserAsync(
            context, "resp@exemplo.test", "Maria Responsável");

        var dto = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand(
                "Troca do switch central", "Conteúdo.", null,
                Type: "Procedure",
                Description: "Como trocar o switch com failover.",
                Tags: ["Redes", " backup ", "REDES"],
                ResponsibleId: responsibleId),
            CancellationToken.None);

        dto.Type.Should().Be("Procedure");
        dto.Description.Should().Be("Como trocar o switch com failover.");
        dto.Tags.Should().Equal("redes", "backup");
        dto.ResponsibleId.Should().Be(responsibleId);
        dto.ResponsibleName.Should().Be("Maria Responsável");
        dto.AuthorId.Should().Be(CurrentUserId);
        dto.AuthorName.Should().Be("Autor do Teste");

        var saved = await context.KnowledgeArticles.AsNoTracking()
            .SingleAsync(article => article.Id == dto.Id);
        saved.Type.Should().Be(KnowledgeArticleType.Procedure);
        saved.AuthorId.Should().Be(CurrentUserId);
        saved.Tags.Should().Equal("redes", "backup");
    }

    [Fact]
    public async Task Create_UnknownResponsible_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand(
                "Artigo", "Conteúdo.", null, ResponsibleId: unknown),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Usuário '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Create_InvalidType_ShouldThrow400PtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand(
                "Artigo", "Conteúdo.", null, Type: "neurótico"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Tipo inválido: 'neurótico'.*");
    }

    [Fact]
    public async Task Update_ShouldReplaceIdentification_KeepingAuthor()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand(
                "Roteiro", "Conteúdo.", null,
                Type: "Procedure", Tags: ["redes"]),
            CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateKnowledgeArticleCommand(
                created.Id, "Roteiro", "Conteúdo novo.", null,
                Reason: "Reclassificado como manual.",
                Type: "Manual",
                Description: "Agora é manual.",
                Tags: ["manual", "passo a passo"],
                ResponsibleId: null),
            CancellationToken.None);

        var updated = await context.KnowledgeArticles.AsNoTracking()
            .SingleAsync(article => article.Id == created.Id);
        updated.Type.Should().Be(KnowledgeArticleType.Manual);
        updated.Description.Should().Be("Agora é manual.");
        updated.Tags.Should().Equal("manual", "passo a passo");
        updated.ResponsibleId.Should().BeNull();
        updated.AuthorId.Should().Be(CurrentUserId); // autor imutável
    }

    [Fact]
    public async Task Search_ShouldReturnIdentificationAndNames()
    {
        await using var context = await CreateContextAsync();
        var responsibleId = await SeedUserAsync(
            context, "resp2@exemplo.test", "João Responsável");
        await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand(
                "Guia de failover", "Conteúdo.", null,
                Type: "Guide",
                Description: "Guia rápido de failover.",
                Tags: ["switch"],
                ResponsibleId: responsibleId),
            CancellationToken.None);

        var page = await SearchHandler(context).Handle(
            new SearchKnowledgeArticlesQuery("failover"), CancellationToken.None);

        var item = page.Items.Should().ContainSingle().Subject;
        item.Type.Should().Be("Guide");
        item.Description.Should().Be("Guia rápido de failover.");
        item.Tags.Should().Equal("switch");
        item.AuthorName.Should().Be("Autor do Teste");
        item.ResponsibleName.Should().Be("João Responsável");
    }
}

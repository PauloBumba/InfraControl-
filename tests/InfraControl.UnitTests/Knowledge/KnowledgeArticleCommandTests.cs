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

    private static CreateKnowledgeArticleCommandHandler CreateHandler(
        AppDbContext context, ICurrentUserContext? currentUser = null)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeCategoryRepository(context),
            currentUser ?? new FakeCurrentUser(["Operator"]),
            NullLogger<CreateKnowledgeArticleCommandHandler>.Instance);

    private static UpdateKnowledgeArticleCommandHandler UpdateHandler(
        AppDbContext context, ICurrentUserContext? currentUser = null)
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

    private sealed class FakeCurrentUser(string[] roles, Guid? userId = null) : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId ?? Guid.NewGuid();
        public string? UserName => "usuario@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => roles;
    }

    private static SearchKnowledgeArticlesQueryHandler SearchHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleOriginRepository(context),
            new FakeCurrentUser(["Operator"]));

    private static GetKnowledgeArticleQueryHandler GetHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleDeviceRepository(context),
            new KnowledgeArticleIncidentRepository(context),
            new KnowledgeArticleServiceRepository(context),
            new KnowledgeArticleOriginRepository(context),
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
                "Troca do switch central", "Failover passo a passo.", categoryId, "Procedimento"),
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
            new CreateKnowledgeArticleCommand("Artigo", "Conteúdo.", unknown, "Guia"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Categoria '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Create_EmptyTitle_ShouldThrowPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("  ", "Conteúdo.", null, "Procedimento"),
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
            new CreateKnowledgeArticleCommand("Roteiro de backup", "Conteúdo.", null, "Manual"),
            CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateKnowledgeArticleCommand(
created.Id, "Roteiro de backup diário", "Conteúdo novo.", categoryId, "Manual",
                Reason: "Revisão de conteúdo."),

                

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

            new UpdateKnowledgeArticleCommand(unknown, "Título", "Conteúdo.", null, "Guia",
                Reason: "Revisão de conteúdo."),

            new UpdateKnowledgeArticleCommand(
                unknown, "Título", "Conteúdo.", null, "Motivo."),
    new UpdateKnowledgeArticleCommand(unknown, "Título", "Conteúdo.", null, "Guia"),

            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Update_UnknownCategory_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Artigo", "Conteúdo.", null, "FAQ"),
            CancellationToken.None);
        var unknown = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(

            new UpdateKnowledgeArticleCommand(created.Id, "Artigo", "Conteúdo.", unknown, "FAQ",
                Reason: "Revisão de conteúdo."),

            new UpdateKnowledgeArticleCommand(
                created.Id, "Artigo", "Conteúdo.", unknown, "Motivo."),

            new UpdateKnowledgeArticleCommand(created.Id, "Artigo", "Conteúdo.", unknown, "FAQ"),


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
            new CreateKnowledgeArticleCommand("Troca do switch central", "Conteúdo A.", null, "Guia"),
            CancellationToken.None);
        var other = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Roteiro de backup", "Conteúdo B.", null, "Guia"),
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


    private static async Task<Guid> SeedUserAsync(AppDbContext context, string name)
    {
        var user = User.Create($"{name.ToLowerInvariant()}@exemplo.test", name, UserRole.Viewer);
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

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

        var responsibleId = await SeedUserAsync(context, "Responsável KB");
        var authorId = Guid.NewGuid();

        var dto = await CreateHandler(context, new FakeCurrentUser(["Operator"], authorId))
            .Handle(new CreateKnowledgeArticleCommand(
                "Guia de failover", "Passo a passo.", null,
                Type: "Guia",
                Description: "Guia de failover do switch central.",
                Tags: ["Redes", "switch"],
                ResponsibleId: responsibleId),
            CancellationToken.None);

        dto.Type.Should().Be("Guia");
        dto.Description.Should().Be("Guia de failover do switch central.");
        dto.AuthorId.Should().Be(authorId);
        dto.ResponsibleId.Should().Be(responsibleId);
        dto.Tags.Should().Equal("redes", "switch");

        var saved = await context.KnowledgeArticles.AsNoTracking()
            .Include(article => article.Tags)
            .SingleAsync(article => article.Id == dto.Id);
        saved.Type.Should().Be("Guia");
        saved.AuthorId.Should().Be(authorId);
        saved.Tags.Select(tag => tag.Tag).Should().BeEquivalentTo("redes", "switch");

    }

    [Fact]
    public async Task Create_UnknownResponsible_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand(

                "Artigo", "Conteúdo.", null, ResponsibleId: unknown),

                "Artigo", "Conteúdo.", null, "Guia",
        

            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Usuário '{unknown}' não encontrado.");
    }

    [Fact]

 

    public async Task Create_UnknownType_ShouldThrowPtBr()
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

            new CreateKnowledgeArticleCommand("Artigo", "Conteúdo.", null, "Receita"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Tipo inválido: 'Receita'. Use:*");
    }

    [Fact]
    public async Task Update_ShouldReplaceTagsAndResponsible()
    {
        await using var context = await CreateContextAsync();
        var first = await SeedUserAsync(context, "Primeiro Responsável");
        var second = await SeedUserAsync(context, "Segundo Responsável");
        var created = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand(
                "Artigo com tags", "Conteúdo.", null, "Guia",
                Tags: ["antiga"],
                ResponsibleId: first),

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

                created.Id, "Artigo com tags", "Conteúdo.", null, "Guia",
                Description: "Nova descrição",
                Tags: ["Nova"],
                ResponsibleId: second,
                Reason: "Revisão de conteúdo."),

            CancellationToken.None);

        var updated = await context.KnowledgeArticles.AsNoTracking()
            .Include(article => article.Tags)
            .SingleAsync(article => article.Id == created.Id);
        updated.Description.Should().Be("Nova descrição");
        updated.ResponsibleId.Should().Be(second);
        updated.Tags.Select(tag => tag.Tag).Should().Equal("nova");
    }

    [Fact]
    public async Task Update_UnknownResponsible_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Artigo", "Conteúdo.", null, "Guia"),
            CancellationToken.None);
        var unknown = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(
            new UpdateKnowledgeArticleCommand(
                created.Id, "Artigo", "Conteúdo.", null, "Guia",
                ResponsibleId: unknown,
                Reason: "Revisão de conteúdo."),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Usuário '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Get_ShouldResolveAuthorAndResponsibleNames()
    {
        await using var context = await CreateContextAsync();
        var responsibleId = await SeedUserAsync(context, "Ana Responsável");
        var authorId = await SeedUserAsync(context, "Bruno Autor");
        var created = await CreateHandler(context, new FakeCurrentUser(["Operator"], authorId))
            .Handle(new CreateKnowledgeArticleCommand(
                "Artigo assinado", "Conteúdo.", null, "Guia",
                ResponsibleId: responsibleId),
            CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(created.Id), CancellationToken.None);

        dto.AuthorName.Should().Be("Bruno Autor");
        dto.ResponsibleName.Should().Be("Ana Responsável");
    }

    /// <summary>K3: PUT em artigo publicado gera snapshot (1,0) com autor+motivo.</summary>
    [Fact]
    public async Task Update_PublishedArticle_ShouldCreateVersionSnapshot()
    {
        await using var context = await CreateContextAsync();
        var authorId = Guid.NewGuid();
        var created = await CreateHandler(context, new FakeCurrentUser(["Operator"], authorId))
            .Handle(new CreateKnowledgeArticleCommand(
                "Guia publicado", "Conteúdo original.", null, "Guia"),
            CancellationToken.None);

        var entity = await context.KnowledgeArticles.SingleAsync(
            article => article.Id == created.Id);
        entity.TransitionTo(KnowledgeArticleStatus.InReview);
        entity.TransitionTo(KnowledgeArticleStatus.Approved);
        entity.TransitionTo(KnowledgeArticleStatus.Published);
        await context.SaveChangesAsync();

        await UpdateHandler(context, new FakeCurrentUser(["Operator"], authorId))
            .Handle(new UpdateKnowledgeArticleCommand(
                created.Id, "Guia publicado", "Conteúdo revisado.", null, "Guia",
                Reason: "Atualização pós-publicação."),
            CancellationToken.None);

        var version = await context.KnowledgeArticleVersions.SingleAsync();
        version.Major.Should().Be(1);
        version.Minor.Should().Be(0);
        version.Version.Should().Be("1.0");
        version.Content.Should().Be("Conteúdo revisado.");
        version.AuthorId.Should().Be(authorId);
        version.Reason.Should().Be("Atualização pós-publicação.");
        version.ApprovedById.Should().BeNull();
    }

    [Fact]
    public async Task Update_DraftArticle_ShouldNotCreateVersion()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Rascunho", "Conteúdo.", null, "Guia"),
            CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateKnowledgeArticleCommand(
                created.Id, "Rascunho", "Conteúdo v2.", null, "Guia",
                Reason: "Ajuste antes de publicar."),
            CancellationToken.None);

        (await context.KnowledgeArticleVersions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Update_MissingReason_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateKnowledgeArticleCommand("Artigo", "Conteúdo.", null, "Guia"),
            CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateKnowledgeArticleCommand(
                created.Id, "Artigo", "Conteúdo.", null, "Guia", Reason: " "),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O motivo da alteração é obrigatório.*");
    }

    [Fact]
    public async Task Search_ShouldReturnTagsAndAuthorName()
    {
        await using var context = await CreateContextAsync();
        var authorId = await SeedUserAsync(context, "Carla Autora");
        await CreateHandler(context, new FakeCurrentUser(["Operator"], authorId))
            .Handle(new CreateKnowledgeArticleCommand(
                "Troca do switch central", "Conteúdo A.", null, "Guia",
                Tags: ["Redes"]),
            CancellationToken.None);

        var page = await SearchHandler(context).Handle(
            new SearchKnowledgeArticlesQuery("switch"), CancellationToken.None);

        var dto = page.Items.Single();
        dto.Type.Should().Be("Guia");
        dto.Tags.Should().Equal("redes");
        dto.AuthorName.Should().Be("Carla Autora");

    }
}

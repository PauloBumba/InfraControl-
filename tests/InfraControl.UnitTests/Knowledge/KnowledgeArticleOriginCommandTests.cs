using FluentAssertions;
using InfraControl.Application.Knowledge.Articles;
using InfraControl.Application.Knowledge.Articles.Commands;
using InfraControl.Application.Knowledge.Articles.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Knowledge;

/// <summary>
/// F24-T004/K4: rastreabilidade de origem (spec docs/32 §9) —
/// múltiplas origens polimórficas (Task/PDI/Chamado) no artigo, com
/// 400 (tipo), 404 (artigo/alvo), 409 (duplicado) em pt-BR e as
/// origens embutidas no GET (detalhe) e na busca (lote, sem N+1).
/// </summary>
public sealed class KnowledgeArticleOriginCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public KnowledgeArticleOriginCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    // Autor do artigo precisa existir (FK author_id SetNull).
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

    private static LinkKnowledgeArticleOriginCommandHandler LinkHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleOriginRepository(context),
            new TaskItemRepository(context),
            new PdiRepository(context),
            new ProjectRepository(context),
            new IncidentRepository(context),
            NullLogger<LinkKnowledgeArticleOriginCommandHandler>.Instance);

    private static UnlinkKnowledgeArticleOriginCommandHandler UnlinkHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleOriginRepository(context),
            NullLogger<UnlinkKnowledgeArticleOriginCommandHandler>.Instance);

    private static GetKnowledgeArticleQueryHandler GetHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleDeviceRepository(context),
            new KnowledgeArticleIncidentRepository(context),
            new KnowledgeArticleServiceRepository(context),
            new KnowledgeArticleOriginRepository(context),
            new FakeCurrentUser(["Operator"]));

    private static SearchKnowledgeArticlesQueryHandler SearchHandler(AppDbContext context)
        => new(
            new KnowledgeArticleRepository(context),
            new KnowledgeArticleOriginRepository(context),
            new FakeCurrentUser(["Operator"]));

    private static async Task<Guid> SeedArticleAsync(AppDbContext context)
    {
        var dto = await new CreateKnowledgeArticleCommandHandler(
                new KnowledgeArticleRepository(context),
                new KnowledgeCategoryRepository(context),
                new KnowledgeArticleVersionRepository(context),
                new FakeCurrentUser(["Operator"]),
                NullLogger<CreateKnowledgeArticleCommandHandler>.Instance)
            .Handle(
                new CreateKnowledgeArticleCommand("Roteiro", "Conteúdo.", null),
                CancellationToken.None);
        return dto.Id;
    }

    private static async Task<Guid> SeedTaskAsync(AppDbContext context)
    {
        var task = TaskItem.Create("Task de origem", TaskKind.Manual);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return task.Id;
    }

    private static async Task<Guid> SeedIncidentAsync(AppDbContext context)
    {
        var incident = Incident.Create("Chamado de origem", IncidentSeverity.Low);
        context.Incidents.Add(incident);
        await context.SaveChangesAsync();
        return incident.Id;
    }

    private static async Task<Guid> SeedPdiAsync(AppDbContext context)
    {
        // PDI não FK user_id (ADR-029) — o dono pode ser qualquer id.
        var plan = IndividualDevelopmentPlan.Create(
            Guid.NewGuid(),
            "Dominar o sistema.",
            new DateOnly(2027, 6, 30),
            PdiStatus.Draft,
            null, null, null,
            new PdiChangeActor(Guid.NewGuid(), "gestor@empresa.test", "dono@empresa.test"));
        context.IndividualDevelopmentPlans.Add(plan);
        await context.SaveChangesAsync();
        return plan.Id;
    }

    private static async Task<Guid> SeedProjectAsync(AppDbContext context)
    {
        var project = Project.Create(
            "PRJ-002",
            "Migração do ambiente de produção",
            "Migrar o ambiente para a nova topologia de rede resiliente.",
            Guid.NewGuid(),
            ProjectPriority.High);

        project.TransitionTo(ProjectStatus.Planned);
        project.TransitionTo(ProjectStatus.InProgress);
        project.SubmitForApproval();
        project.Approve(Guid.NewGuid(), "Homologado pela direção.", DateTimeOffset.UtcNow);

        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project.Id;
    }

    [Fact]
    public async Task Link_TaskOrigin_ShouldAppearInDetailAndSearch()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);
        var taskId = await SeedTaskAsync(context);

        await LinkHandler(context).Handle(
            new LinkKnowledgeArticleOriginCommand(articleId, "task", taskId),
            CancellationToken.None);

        var detail = await GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);
        detail.Origins.Should().ContainSingle()
            .Which.Should().Be(new KnowledgeArticleOriginDto("Task", taskId));

        var page = await SearchHandler(context).Handle(
            new SearchKnowledgeArticlesQuery(), CancellationToken.None);
        page.Items.Single().Origins.Should().ContainSingle()
            .Which.Should().Be(new KnowledgeArticleOriginDto("Task", taskId));
    }

    /// <summary>§9: o artigo pode ter múltiplas origens.</summary>
    [Fact]
    public async Task Link_MultipleOrigins_ShouldKeepAllTypes()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);
        var taskId = await SeedTaskAsync(context);
        var pdiId = await SeedPdiAsync(context);
        var projectId = await SeedProjectAsync(context);
        var incidentId = await SeedIncidentAsync(context);

        await LinkHandler(context).Handle(
            new LinkKnowledgeArticleOriginCommand(articleId, "task", taskId),
            CancellationToken.None);
        await LinkHandler(context).Handle(
            new LinkKnowledgeArticleOriginCommand(articleId, "pdi", pdiId),
            CancellationToken.None);
        await LinkHandler(context).Handle(
            new LinkKnowledgeArticleOriginCommand(articleId, "project", projectId),
            CancellationToken.None);
        await LinkHandler(context).Handle(
            new LinkKnowledgeArticleOriginCommand(articleId, "incident", incidentId),
            CancellationToken.None);

        var detail = await GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);

        detail.Origins.Should().BeEquivalentTo(new[]
        {
            new KnowledgeArticleOriginDto("Task", taskId),
            new KnowledgeArticleOriginDto("Pdi", pdiId),
            new KnowledgeArticleOriginDto("Project", projectId),
            new KnowledgeArticleOriginDto("Incident", incidentId),
        });
    }

    [Fact]
    public async Task Link_InvalidType_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);

        var action = () => LinkHandler(context).Handle(
            new LinkKnowledgeArticleOriginCommand(articleId, "projeto", Guid.NewGuid()),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage(
                "Origem inválida: 'projeto'. Use 'task', 'pdi', 'project' ou 'incident'.*");
    }

    [Fact]
    public async Task Link_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);
        var unknown = Guid.NewGuid();

        var action = () => LinkHandler(context).Handle(
            new LinkKnowledgeArticleOriginCommand(articleId, "task", unknown),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Link_UnknownArticle_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var unknown = Guid.NewGuid();

        var action = () => LinkHandler(context).Handle(
            new LinkKnowledgeArticleOriginCommand(unknown, "task", taskId),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Link_Duplicate_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);
        var taskId = await SeedTaskAsync(context);
        await LinkHandler(context).Handle(
            new LinkKnowledgeArticleOriginCommand(articleId, "task", taskId),
            CancellationToken.None);

        var action = () => LinkHandler(context).Handle(
            new LinkKnowledgeArticleOriginCommand(articleId, "task", taskId),
            CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("A origem já está vinculada ao artigo.");
    }

    [Fact]
    public async Task Unlink_ShouldRemoveOrigin()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);
        var taskId = await SeedTaskAsync(context);
        await LinkHandler(context).Handle(
            new LinkKnowledgeArticleOriginCommand(articleId, "task", taskId),
            CancellationToken.None);

        await UnlinkHandler(context).Handle(
            new UnlinkKnowledgeArticleOriginCommand(articleId, "task", taskId),
            CancellationToken.None);

        var detail = await GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);
        detail.Origins.Should().BeEmpty();
    }

    [Fact]
    public async Task Unlink_UnknownLink_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var articleId = await SeedArticleAsync(context);
        var taskId = await SeedTaskAsync(context);

        var action = () => UnlinkHandler(context).Handle(
            new UnlinkKnowledgeArticleOriginCommand(articleId, "task", taskId),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage(
                $"Origem 'task' '{taskId}' não encontrada para o artigo '{articleId}'.");
    }
}

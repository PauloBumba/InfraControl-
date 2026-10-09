using FluentAssertions;
using InfraControl.Application.Tasks;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Tasks;

/// <summary>
/// F23-T005/P5a: vínculo Task ↔ Knowledge — 404 pt-BR (task/artigo/
/// vínculo), 409 em duplicidade, TaskDto com articleIds e histórico
/// KnowledgeLinked/KnowledgeUnlinked na timeline tipo "vinculo".
/// </summary>
public sealed class TaskArticleCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskArticleCommandTests()
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

    private static TaskItemRepository TaskRepo(AppDbContext context) => new(context);
    private static TaskAssigneeRepository AssigneeRepo(AppDbContext context) => new(context);
    private static TaskDeviceRepository DeviceRepo(AppDbContext context) => new(context);
    private static TaskIncidentRepository IncidentLinkRepo(AppDbContext context) => new(context);
    private static TaskKnowledgeArticleRepository KnowledgeLinkRepo(AppDbContext context) => new(context);
    private static TaskPdiRepository PdiLinkRepo(AppDbContext context) => new(context);
    private static KnowledgeArticleRepository ArticleRepo(AppDbContext context) => new(context);

    private static async Task<(Guid TaskId, Guid ArticleId)> SeedTaskAndArticleAsync(
        AppDbContext context)
    {
        var task = TaskItem.Create("Tarefa com knowledge", TaskKind.Manual);
        context.Tasks.Add(task);

        var article = KnowledgeArticle.Create(
            "Configuração SNMP de impressoras",
            "Procedimento de configuração SNMP.", null);
        context.KnowledgeArticles.Add(article);
        await context.SaveChangesAsync();
        return (task.Id, article.Id);
    }

    private static RecordingTaskHistoryWriter History() => new();

    private static LinkTaskArticleCommandHandler LinkHandler(AppDbContext context)
        => new(TaskRepo(context), ArticleRepo(context), KnowledgeLinkRepo(context),
            History(), NullLogger<LinkTaskArticleCommandHandler>.Instance);

    private static UnlinkTaskArticleCommandHandler UnlinkHandler(AppDbContext context)
        => new(TaskRepo(context), KnowledgeLinkRepo(context), History(),
            NullLogger<UnlinkTaskArticleCommandHandler>.Instance);

    private static GetTaskQueryHandler GetHandler(AppDbContext context)
        => new(TaskRepo(context), AssigneeRepo(context), DeviceRepo(context),
            IncidentLinkRepo(context), KnowledgeLinkRepo(context), PdiLinkRepo(context));

    // ── Link ─────────────────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireTasksManage()
    {
        new LinkTaskArticleCommand(Guid.NewGuid(), Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new UnlinkTaskArticleCommand(Guid.NewGuid(), Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
    }

    [Fact]
    public async Task Link_ShouldExposeArticleInTaskDto()
    {
        await using var context = await CreateContextAsync();
        var (taskId, articleId) = await SeedTaskAndArticleAsync(context);

        await LinkHandler(context).Handle(
            new LinkTaskArticleCommand(taskId, articleId), CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetTaskQuery(taskId), CancellationToken.None);
        dto.ArticleCount.Should().Be(1);
        dto.ArticleIds.Should().Equal(articleId);
    }

    [Fact]
    public async Task Link_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (_, articleId) = await SeedTaskAndArticleAsync(context);
        var unknown = Guid.NewGuid();

        var action = () => LinkHandler(context).Handle(
            new LinkTaskArticleCommand(unknown, articleId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Link_UnknownArticle_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedTaskAndArticleAsync(context);
        var unknown = Guid.NewGuid();

        var action = () => LinkHandler(context).Handle(
            new LinkTaskArticleCommand(taskId, unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Link_Duplicate_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, articleId) = await SeedTaskAndArticleAsync(context);

        await LinkHandler(context).Handle(
            new LinkTaskArticleCommand(taskId, articleId), CancellationToken.None);

        var action = () => LinkHandler(context).Handle(
            new LinkTaskArticleCommand(taskId, articleId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("O artigo já está vinculado à task.");
    }

    [Fact]
    public async Task LinkAndUnlink_ShouldRecordHistoryActions()
    {
        await using var context = await CreateContextAsync();
        var (taskId, articleId) = await SeedTaskAndArticleAsync(context);
        var history = History();

        await new LinkTaskArticleCommandHandler(
                TaskRepo(context), ArticleRepo(context), KnowledgeLinkRepo(context),
                history, NullLogger<LinkTaskArticleCommandHandler>.Instance)
            .Handle(new LinkTaskArticleCommand(taskId, articleId), CancellationToken.None);
        await new UnlinkTaskArticleCommandHandler(
                TaskRepo(context), KnowledgeLinkRepo(context), history,
                NullLogger<UnlinkTaskArticleCommandHandler>.Instance)
            .Handle(new UnlinkTaskArticleCommand(taskId, articleId), CancellationToken.None);

        history.Records.Should().Equal(
            (taskId, TaskHistoryAction.KnowledgeLinked, articleId.ToString()),
            (taskId, TaskHistoryAction.KnowledgeUnlinked, articleId.ToString()));
    }

    // ── Unlink ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Unlink_ShouldRemoveLinkAndClearDto()
    {
        await using var context = await CreateContextAsync();
        var (taskId, articleId) = await SeedTaskAndArticleAsync(context);
        await LinkHandler(context).Handle(
            new LinkTaskArticleCommand(taskId, articleId), CancellationToken.None);

        await UnlinkHandler(context).Handle(
            new UnlinkTaskArticleCommand(taskId, articleId), CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetTaskQuery(taskId), CancellationToken.None);
        dto.ArticleCount.Should().Be(0);
        dto.ArticleIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Unlink_UnknownLink_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedTaskAndArticleAsync(context);
        var stranger = Guid.NewGuid();

        var action = () => UnlinkHandler(context).Handle(
            new UnlinkTaskArticleCommand(taskId, stranger), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Vínculo da task '{taskId}' com o artigo "
                + $"'{stranger}' não encontrado.");
    }

    // ── Integração com Search (F7-T001) ──────────────────────────────────

    [Fact]
    public async Task Search_ShouldEnrichPageWithArticleIds()
    {
        await using var context = await CreateContextAsync();
        var (taskId, articleId) = await SeedTaskAndArticleAsync(context);
        await LinkHandler(context).Handle(
            new LinkTaskArticleCommand(taskId, articleId), CancellationToken.None);

        var handler = new SearchTasksQueryHandler(
            TaskRepo(context), AssigneeRepo(context), DeviceRepo(context),
            IncidentLinkRepo(context), KnowledgeLinkRepo(context), PdiLinkRepo(context));
        var page = await handler.Handle(
            new SearchTasksQuery(), CancellationToken.None);

        var item = page.Items.Should().ContainSingle().Subject;
        item.ArticleCount.Should().Be(1);
        item.ArticleIds.Should().Equal(articleId);
    }

    // ── Timeline (F23-T004/P4d) ──────────────────────────────────────────

    [Fact]
    public void KnowledgeActions_ShouldMapToLinkTimelineType()
    {
        TaskTimelineTypes.ForAction(TaskHistoryAction.KnowledgeLinked)
            .Should().Be(TaskTimelineTypes.Link);
        TaskTimelineTypes.ForAction(TaskHistoryAction.KnowledgeUnlinked)
            .Should().Be(TaskTimelineTypes.Link);
    }
}

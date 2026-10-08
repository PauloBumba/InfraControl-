using FluentAssertions;
using InfraControl.Application.Tasks;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Tasks;

/// <summary>
/// F23-T005/P5b: vínculo Task ↔ PDI (plano do usuário) — 404 pt-BR
/// (task/PDI/vínculo), 409 em duplicidade, TaskDto com pdiUserIds e
/// histórico PdiLinked/PdiUnlinked na timeline tipo "vinculo".
/// </summary>
public sealed class TaskPdiCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskPdiCommandTests()
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
    private static PdiRepository PdiRepo(AppDbContext context) => new(context);

    private static async Task<(Guid TaskId, Guid UserId)> SeedTaskAndPdiAsync(
        AppDbContext context)
    {
        var task = TaskItem.Create("Tarefa com PDI", TaskKind.Manual);
        context.Tasks.Add(task);

        // PDI não FK user_id (ADR-029) — o dono pode ser qualquer id.
        var ownerId = Guid.NewGuid();
        var plan = IndividualDevelopmentPlan.Create(
            ownerId,
            "Dominar o funcionamento do sistema de Tomografia.",
            new DateOnly(2027, 6, 30),
            PdiStatus.Draft,
            null, null, null,
            new PdiChangeActor(
                Guid.NewGuid(), "gestor@empresa.test", "dono@empresa.test"));
        context.IndividualDevelopmentPlans.Add(plan);
        await context.SaveChangesAsync();
        return (task.Id, ownerId);
    }

    private static RecordingTaskHistoryWriter History() => new();

    private static LinkTaskPdiCommandHandler LinkHandler(AppDbContext context)
        => new(TaskRepo(context), PdiRepo(context), PdiLinkRepo(context),
            History(), NullLogger<LinkTaskPdiCommandHandler>.Instance);

    private static UnlinkTaskPdiCommandHandler UnlinkHandler(AppDbContext context)
        => new(TaskRepo(context), PdiLinkRepo(context), History(),
            NullLogger<UnlinkTaskPdiCommandHandler>.Instance);

    private static GetTaskQueryHandler GetHandler(AppDbContext context)
        => new(TaskRepo(context), AssigneeRepo(context), DeviceRepo(context),
            IncidentLinkRepo(context), KnowledgeLinkRepo(context),
            PdiLinkRepo(context));

    // ── Link ─────────────────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireTasksManage()
    {
        new LinkTaskPdiCommand(Guid.NewGuid(), Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new UnlinkTaskPdiCommand(Guid.NewGuid(), Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
    }

    [Fact]
    public async Task Link_ShouldExposePdiInTaskDto()
    {
        await using var context = await CreateContextAsync();
        var (taskId, userId) = await SeedTaskAndPdiAsync(context);

        await LinkHandler(context).Handle(
            new LinkTaskPdiCommand(taskId, userId), CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetTaskQuery(taskId), CancellationToken.None);
        dto.PdiCount.Should().Be(1);
        dto.PdiUserIds.Should().Equal(userId);
    }

    [Fact]
    public async Task Link_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (_, userId) = await SeedTaskAndPdiAsync(context);
        var unknown = Guid.NewGuid();

        var action = () => LinkHandler(context).Handle(
            new LinkTaskPdiCommand(unknown, userId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Link_UnknownPdi_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedTaskAndPdiAsync(context);
        var unknown = Guid.NewGuid();

        var action = () => LinkHandler(context).Handle(
            new LinkTaskPdiCommand(taskId, unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"PDI do usuário '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Link_Duplicate_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, userId) = await SeedTaskAndPdiAsync(context);

        await LinkHandler(context).Handle(
            new LinkTaskPdiCommand(taskId, userId), CancellationToken.None);

        var action = () => LinkHandler(context).Handle(
            new LinkTaskPdiCommand(taskId, userId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("O PDI já está vinculado à task.");
    }

    [Fact]
    public async Task LinkAndUnlink_ShouldRecordHistoryActions()
    {
        await using var context = await CreateContextAsync();
        var (taskId, userId) = await SeedTaskAndPdiAsync(context);
        var history = History();

        await new LinkTaskPdiCommandHandler(
                TaskRepo(context), PdiRepo(context), PdiLinkRepo(context),
                history, NullLogger<LinkTaskPdiCommandHandler>.Instance)
            .Handle(new LinkTaskPdiCommand(taskId, userId), CancellationToken.None);
        await new UnlinkTaskPdiCommandHandler(
                TaskRepo(context), PdiLinkRepo(context), history,
                NullLogger<UnlinkTaskPdiCommandHandler>.Instance)
            .Handle(new UnlinkTaskPdiCommand(taskId, userId), CancellationToken.None);

        history.Records.Should().Equal(
            (taskId, TaskHistoryAction.PdiLinked, userId.ToString()),
            (taskId, TaskHistoryAction.PdiUnlinked, userId.ToString()));
    }

    // ── Unlink ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Unlink_ShouldRemoveLinkAndClearDto()
    {
        await using var context = await CreateContextAsync();
        var (taskId, userId) = await SeedTaskAndPdiAsync(context);
        await LinkHandler(context).Handle(
            new LinkTaskPdiCommand(taskId, userId), CancellationToken.None);

        await UnlinkHandler(context).Handle(
            new UnlinkTaskPdiCommand(taskId, userId), CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetTaskQuery(taskId), CancellationToken.None);
        dto.PdiCount.Should().Be(0);
        dto.PdiUserIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Unlink_UnknownLink_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedTaskAndPdiAsync(context);
        var stranger = Guid.NewGuid();

        var action = () => UnlinkHandler(context).Handle(
            new UnlinkTaskPdiCommand(taskId, stranger), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Vínculo da task '{taskId}' com o PDI do usuário "
                + $"'{stranger}' não encontrado.");
    }

    // ── Integração com Search (F7-T001) ──────────────────────────────────

    [Fact]
    public async Task Search_ShouldEnrichPageWithPdiUserIds()
    {
        await using var context = await CreateContextAsync();
        var (taskId, userId) = await SeedTaskAndPdiAsync(context);
        await LinkHandler(context).Handle(
            new LinkTaskPdiCommand(taskId, userId), CancellationToken.None);

        var handler = new SearchTasksQueryHandler(
            TaskRepo(context), AssigneeRepo(context), DeviceRepo(context),
            IncidentLinkRepo(context), KnowledgeLinkRepo(context),
            PdiLinkRepo(context));
        var page = await handler.Handle(
            new SearchTasksQuery(), CancellationToken.None);

        var item = page.Items.Should().ContainSingle().Subject;
        item.PdiCount.Should().Be(1);
        item.PdiUserIds.Should().Equal(userId);
    }

    // ── Timeline (F23-T004/P4d) ──────────────────────────────────────────

    [Fact]
    public void PdiActions_ShouldMapToLinkTimelineType()
    {
        TaskTimelineTypes.ForAction(TaskHistoryAction.PdiLinked)
            .Should().Be(TaskTimelineTypes.Link);
        TaskTimelineTypes.ForAction(TaskHistoryAction.PdiUnlinked)
            .Should().Be(TaskTimelineTypes.Link);
    }
}

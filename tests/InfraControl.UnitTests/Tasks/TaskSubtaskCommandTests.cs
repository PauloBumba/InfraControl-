using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Tasks;

/// <summary>
/// F23-T004/P4a — subtarefas e progresso: criação com pai (404/400
/// pt-BR), movimentação/desvinculação via SetTaskParent (histórico em 3
/// frentes: filho ParentChanged + pais SubtaskAdded/Removed), filtros
/// parentId/rootOnly do Search e progresso derivado no DTO (concluídas ÷
/// não canceladas, sem subtarefas ativas → null).
/// </summary>
public sealed class TaskSubtaskCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskSubtaskCommandTests()
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

    private static TaskItemRepository Repo(AppDbContext context) => new(context);
    private static TaskAssigneeRepository AssigneeRepo(AppDbContext context) => new(context);
    private static TaskDeviceRepository DeviceRepo(AppDbContext context) => new(context);
    private static TaskIncidentRepository IncidentRepo(AppDbContext context) => new(context);

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId;
        public string? UserName => "gestor@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => ["Admin"];
    }

    private static CreateTaskCommandHandler CreateHandler(
        AppDbContext context, RecordingTaskHistoryWriter history,
        Guid? actorId = null)
        => new(
            Repo(context),
            AssigneeRepo(context),
            history,
            new FakeCurrentUser(actorId ?? Guid.NewGuid()),
            NullLogger<CreateTaskCommandHandler>.Instance);

    private static SetTaskParentCommandHandler SetParentHandler(
        AppDbContext context, RecordingTaskHistoryWriter history, Guid actorId)
        => new(
            Repo(context),
            AssigneeRepo(context),
            DeviceRepo(context),
            IncidentRepo(context),
            history,
            new FakeCurrentUser(actorId),
            NullLogger<SetTaskParentCommandHandler>.Instance);

    private static SearchTasksQueryHandler SearchHandler(AppDbContext context)
        => new(Repo(context), AssigneeRepo(context), DeviceRepo(context), IncidentRepo(context), new TaskKnowledgeArticleRepository(context), new TaskPdiRepository(context));

    private static GetTaskQueryHandler GetHandler(AppDbContext context)
        => new(Repo(context), AssigneeRepo(context), DeviceRepo(context), IncidentRepo(context), new TaskKnowledgeArticleRepository(context), new TaskPdiRepository(context));

    private static async Task<Guid> SeedTaskAsync(
        AppDbContext context, string title = "Task raiz")
    {
        var task = TaskItem.Create(title, TaskKind.Manual);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return task.Id;
    }

    /// <summary>Cria uma subtarefa direto no domínio (sem command).</summary>
    private static async Task<Guid> SeedSubtaskAsync(
        AppDbContext context, Guid parentId, string title)
    {
        var task = TaskItem.Create(title, TaskKind.Manual);
        task.SetParent(parentId);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return task.Id;
    }

    // ── Criação com pai ───────────────────────────────────────────────

    [Fact]
    public void SetParentCommand_ShouldRequireOperate()
    {
        new SetTaskParentCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
    }

    [Fact]
    public async Task Create_WithParent_ShouldPairAndRecordParentHistory()
    {
        await using var context = await CreateContextAsync();
        var history = new RecordingTaskHistoryWriter();
        var handler = CreateHandler(context, history);
        var parentId = await handler.Handle(
            new CreateTaskCommand("Pai da subtarefa", "Project"),
            CancellationToken.None);
        history.Records.Clear();

        var dto = await handler.Handle(
            new CreateTaskCommand(
                "Subtarefa", "Manual", ParentTaskId: parentId.Id),
            CancellationToken.None);

        dto.ParentTaskId.Should().Be(parentId.Id);
        dto.ParentTitle.Should().Be("Pai da subtarefa");

        var saved = await context.Tasks.AsNoTracking()
            .SingleAsync(task => task.Title == "Subtarefa");
        saved.ParentTaskId.Should().Be(parentId.Id);

        // Filho nasce com Created; o pai recebe SubtaskAdded (título).
        history.Records.Should().HaveCount(2);
        history.Records[0].Action.Should().Be(TaskHistoryAction.Created);
        history.Records[1].Action.Should().Be(TaskHistoryAction.SubtaskAdded);
        history.Records[1].TaskId.Should().Be(parentId.Id);
        history.Records[1].Detail.Should().Be("Subtarefa");
    }

    [Fact]
    public async Task Create_UnknownParent_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => CreateHandler(context, new RecordingTaskHistoryWriter())
            .Handle(
                new CreateTaskCommand("Órfã", "Manual", ParentTaskId: unknown),
                CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Create_ParentIsSubtask_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var history = new RecordingTaskHistoryWriter();
        var handler = CreateHandler(context, history);
        var rootId = await handler.Handle(
            new CreateTaskCommand("Raiz", "Project"), CancellationToken.None);
        var childId = await handler.Handle(
            new CreateTaskCommand("Filha", "Manual", ParentTaskId: rootId.Id),
            CancellationToken.None);

        var action = () => handler.Handle(
            new CreateTaskCommand(
                "Neta", "Manual", ParentTaskId: childId.Id),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Subtarefas não podem ter subtarefas.*");
    }

    // ── Movimentação / desvinculação ──────────────────────────────────

    [Fact]
    public async Task SetParent_ShouldLinkAndRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var parentId = await SeedTaskAsync(context, "Pai A");
        var childId = await SeedTaskAsync(context, "Filha solteira");
        var history = new RecordingTaskHistoryWriter();

        var dto = await SetParentHandler(context, history, Guid.NewGuid())
            .Handle(
                new SetTaskParentCommand(childId, parentId),
                CancellationToken.None);

        dto.ParentTaskId.Should().Be(parentId);

        var saved = await context.Tasks.AsNoTracking()
            .SingleAsync(task => task.Id == childId);
        saved.ParentTaskId.Should().Be(parentId);

        // Filho: "de → para"; pai: SubtaskAdded com o título do filho.
        history.Records.Should().HaveCount(2);
        history.Records[0].Action.Should().Be(TaskHistoryAction.ParentChanged);
        history.Records[0].TaskId.Should().Be(childId);
        history.Records[0].Detail.Should().Be("sem pai → Pai A");
        history.Records[1].Action.Should().Be(TaskHistoryAction.SubtaskAdded);
        history.Records[1].TaskId.Should().Be(parentId);
        history.Records[1].Detail.Should().Be("Filha solteira");
    }

    [Fact]
    public async Task SetParent_ShouldMoveBetweenParents()
    {
        await using var context = await CreateContextAsync();
        var parentA = await SeedTaskAsync(context, "Pai A");
        var parentB = await SeedTaskAsync(context, "Pai B");
        var childId = await SeedSubtaskAsync(context, parentA, "Filha");
        var history = new RecordingTaskHistoryWriter();

        await SetParentHandler(context, history, Guid.NewGuid())
            .Handle(
                new SetTaskParentCommand(childId, parentB),
                CancellationToken.None);

        // 3 frentes: filho reassociado; A perde; B ganha.
        history.Records.Should().HaveCount(3);
        history.Records[0].Action.Should().Be(TaskHistoryAction.ParentChanged);
        history.Records[0].Detail.Should().Be("Pai A → Pai B");
        history.Records[1].Action.Should().Be(TaskHistoryAction.SubtaskRemoved);
        history.Records[1].TaskId.Should().Be(parentA);
        history.Records[2].Action.Should().Be(TaskHistoryAction.SubtaskAdded);
        history.Records[2].TaskId.Should().Be(parentB);
        history.Records.Should().OnlyContain(record => record.Detail == "Filha"
            || record.Detail == "Pai A → Pai B");
    }

    [Fact]
    public async Task SetParent_ToNull_ShouldUnlinkAndRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var parentId = await SeedTaskAsync(context, "Pai A");
        var childId = await SeedSubtaskAsync(context, parentId, "Filha");
        var history = new RecordingTaskHistoryWriter();

        await SetParentHandler(context, history, Guid.NewGuid())
            .Handle(new SetTaskParentCommand(childId), CancellationToken.None);

        var saved = await context.Tasks.AsNoTracking()
            .SingleAsync(task => task.Id == childId);
        saved.ParentTaskId.Should().BeNull();

        history.Records.Should().HaveCount(2);
        history.Records[0].Action.Should().Be(TaskHistoryAction.ParentChanged);
        history.Records[0].Detail.Should().Be("Pai A → sem pai");
        history.Records[1].Action.Should().Be(TaskHistoryAction.SubtaskRemoved);
        history.Records[1].TaskId.Should().Be(parentId);
    }

    [Fact]
    public async Task SetParent_ShouldKeepHistoryDetailWithinLimit()
    {
        await using var context = await CreateContextAsync();
        var longA = new string('a', 120);
        var longB = new string('b', 120);
        var parentA = await SeedTaskAsync(context, longA);
        var parentB = await SeedTaskAsync(context, longB);
        var childId = await SeedSubtaskAsync(context, parentA, "Filha");
        var history = new RecordingTaskHistoryWriter();

        await SetParentHandler(context, history, Guid.NewGuid())
            .Handle(
                new SetTaskParentCommand(childId, parentB),
                CancellationToken.None);

        // Truncate(…, 95) devolve 94 chars + "…" → lado direito mede 95.
        var detail = history.Records[0].Detail;
        detail.Should().EndWith("→ " + new string('b', 94) + "…");
        detail!.Length.Should().BeLessThanOrEqualTo(TaskHistory.DetailMaxLength);
    }

    [Fact]
    public async Task SetParent_ToSelf_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);

        var action = () => SetParentHandler(context, new RecordingTaskHistoryWriter(), Guid.NewGuid())
            .Handle(
                new SetTaskParentCommand(taskId, taskId),
                CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Uma task não pode ser própria subtarefa.*");
    }

    [Fact]
    public async Task SetParent_ParentIsSubtask_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var rootId = await SeedTaskAsync(context, "Raiz");
        var childA = await SeedSubtaskAsync(context, rootId, "Filha A");
        var childB = await SeedSubtaskAsync(context, rootId, "Filha B");

        var action = () => SetParentHandler(context, new RecordingTaskHistoryWriter(), Guid.NewGuid())
            .Handle(
                new SetTaskParentCommand(childB, childA),
                CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Subtarefas não podem ter subtarefas.*");
    }

    [Fact]
    public async Task SetParent_UnknownTaskOrParent_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var handler = SetParentHandler(context, new RecordingTaskHistoryWriter(), Guid.NewGuid());
        var unknownTask = Guid.NewGuid();

        var taskAction = () => handler.Handle(
            new SetTaskParentCommand(unknownTask), CancellationToken.None);
        (await taskAction.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknownTask}' não encontrada.");

        var knownTask = await SeedTaskAsync(context);
        var unknownParent = Guid.NewGuid();
        var parentAction = () => handler.Handle(
            new SetTaskParentCommand(knownTask, unknownParent), CancellationToken.None);
        (await parentAction.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknownParent}' não encontrada.");
    }

    [Fact]
    public async Task SetParent_SameParent_ShouldBeNoOpWithoutHistory()
    {
        await using var context = await CreateContextAsync();
        var parentId = await SeedTaskAsync(context, "Pai A");
        var childId = await SeedSubtaskAsync(context, parentId, "Filha");
        var history = new RecordingTaskHistoryWriter();

        var dto = await SetParentHandler(context, history, Guid.NewGuid())
            .Handle(
                new SetTaskParentCommand(childId, parentId),
                CancellationToken.None);

        dto.ParentTaskId.Should().Be(parentId);
        history.Records.Should().BeEmpty();
    }

    // ── Filtros do Search e progresso ─────────────────────────────────

    [Fact]
    public async Task Search_ShouldFilterByParentIdAndRootOnly()
    {
        await using var context = await CreateContextAsync();
        var parentId = await SeedTaskAsync(context, "Pai");
        await SeedSubtaskAsync(context, parentId, "Filha 1");
        await SeedSubtaskAsync(context, parentId, "Filha 2");
        var handler = SearchHandler(context);

        var children = await handler.Handle(
            new SearchTasksQuery(ParentId: parentId), CancellationToken.None);
        children.TotalCount.Should().Be(2);
        children.Items.Should().OnlyContain(task => task.ParentTaskId == parentId);

        var roots = await handler.Handle(
            new SearchTasksQuery(RootOnly: true), CancellationToken.None);
        roots.TotalCount.Should().Be(1);
        roots.Items.Should().ContainSingle().Which.Id.Should().Be(parentId);

        var all = await handler.Handle(
            new SearchTasksQuery(), CancellationToken.None);
        all.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Search_ParentIdWithRootOnly_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => SearchHandler(context).Handle(
            new SearchTasksQuery(ParentId: Guid.NewGuid(), RootOnly: true),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Os filtros rootOnly e parentId são incompatíveis.*");
    }

    [Fact]
    public async Task GetTask_ShouldExposeProgressAndParentTitle()
    {
        await using var context = await CreateContextAsync();
        var parentId = await SeedTaskAsync(context, "Pai com progresso");

        // 3 subtarefas: 1 concluída, 1 cancelada (fora do denominador),
        // 1 aberta → progresso = 100 * 1 / 2 = 50.
        var done = await SeedSubtaskAsync(context, parentId, "Concluída");
        await SeedSubtaskAsync(context, parentId, "Cancelada");
        await SeedSubtaskAsync(context, parentId, "Aberta");

        var doneTask = await context.Tasks.SingleAsync(task => task.Id == done);
        doneTask.ChangeState(TaskState.InProgress);
        doneTask.ChangeState(TaskState.Resolved);
        doneTask.ChangeState(TaskState.Closed);
        var cancelled = await context.Tasks
            .SingleAsync(task => task.Title == "Cancelada");
        cancelled.ChangeState(TaskState.Cancelled);
        await context.SaveChangesAsync();

        var parentDto = await GetHandler(context).Handle(
            new GetTaskQuery(parentId), CancellationToken.None);
        parentDto.SubtaskCount.Should().Be(3);
        parentDto.Progress.Should().Be(50);
        parentDto.ParentTaskId.Should().BeNull();

        // No Search (mesmo cálculo) e na subtarefa (título do pai).
        var page = await SearchHandler(context).Handle(
            new SearchTasksQuery(ParentId: parentId), CancellationToken.None);
        page.Items.Should().OnlyContain(task => task.Progress == null);

        var rootPage = await SearchHandler(context).Handle(
            new SearchTasksQuery(RootOnly: true), CancellationToken.None);
        rootPage.Items.Single().Progress.Should().Be(50);

        var childDto = await GetHandler(context).Handle(
            new GetTaskQuery(done), CancellationToken.None);
        childDto.ParentTitle.Should().Be("Pai com progresso");
        childDto.SubtaskCount.Should().Be(0);
        childDto.Progress.Should().BeNull();
    }

    [Fact]
    public async Task GetTask_WithoutSubtasks_ShouldReturnNullProgress()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context, "Solitária");

        var dto = await GetHandler(context).Handle(
            new GetTaskQuery(taskId), CancellationToken.None);

        dto.SubtaskCount.Should().Be(0);
        dto.Progress.Should().BeNull();
    }
}

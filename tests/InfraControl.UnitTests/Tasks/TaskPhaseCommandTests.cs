using FluentAssertions;
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
/// F23-T004/P4d — fases dentro da task (§7): CRUD no molde dos
/// critérios — 404 pt-BR, 400 do domínio, primeira fase nasce atual,
/// ponteiro único por task (irmão limpo no mesmo PUT), um histórico por
/// aspecto alterado (PhaseAdded/Updated/CurrentChanged/Completed/
/// Reopened/Removed), no-op sem registro, ordem por posição com gaps.
/// </summary>
public sealed class TaskPhaseCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskPhaseCommandTests()
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
    private static TaskPhaseRepository PhaseRepo(AppDbContext context) => new(context);

    private static AddTaskPhaseCommandHandler AddHandler(
        AppDbContext context, RecordingTaskHistoryWriter history)
        => new(
            Repo(context),
            PhaseRepo(context),
            history,
            NullLogger<AddTaskPhaseCommandHandler>.Instance);

    private static UpdateTaskPhaseCommandHandler UpdateHandler(
        AppDbContext context, RecordingTaskHistoryWriter history)
        => new(
            PhaseRepo(context),
            history,
            NullLogger<UpdateTaskPhaseCommandHandler>.Instance);

    private static DeleteTaskPhaseCommandHandler DeleteHandler(
        AppDbContext context, RecordingTaskHistoryWriter history)
        => new(
            PhaseRepo(context),
            history,
            NullLogger<DeleteTaskPhaseCommandHandler>.Instance);

    private static GetTaskPhasesQueryHandler QueryHandler(AppDbContext context)
        => new(Repo(context), PhaseRepo(context));

    private static async Task<Guid> SeedTaskAsync(AppDbContext context)
    {
        var task = TaskItem.Create("Task para fases", TaskKind.Incident);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return task.Id;
    }

    private static async Task<Guid> SeedPhaseAsync(
        AppDbContext context, Guid taskId, string name, int position)
    {
        var phase = TaskPhase.Create(taskId, name, position);
        context.TaskPhases.Add(phase);
        await context.SaveChangesAsync();
        return phase.Id;
    }

    // ── Roles ─────────────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireOperateAndQueryRead()
    {
        new AddTaskPhaseCommand(Guid.NewGuid(), "x").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new UpdateTaskPhaseCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new DeleteTaskPhaseCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new GetTaskPhasesQuery(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Read);
    }

    // ── Add ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Add_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => AddHandler(context, new RecordingTaskHistoryWriter())
            .Handle(new AddTaskPhaseCommand(unknown, "Fase"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Add_FirstPhase_ShouldBecomeCurrentAtPositionZero()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var history = new RecordingTaskHistoryWriter();

        var dto = await AddHandler(context, history).Handle(
            new AddTaskPhaseCommand(taskId, "Diagnóstico"), CancellationToken.None);

        dto.Position.Should().Be(0);
        dto.IsCurrent.Should().BeTrue(); // primeira nasce atual
        dto.CompletedAt.Should().BeNull();

        history.Records.Should().ContainSingle();
        history.Records[0].Action.Should().Be(TaskHistoryAction.PhaseAdded);
        history.Records[0].Detail.Should().Be("Diagnóstico");
    }

    [Fact]
    public async Task Add_SecondPhase_ShouldAppendAndStayNonCurrent()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var history = new RecordingTaskHistoryWriter();

        await AddHandler(context, history).Handle(
            new AddTaskPhaseCommand(taskId, "Primeira"), CancellationToken.None);
        history.Records.Clear();
        var second = await AddHandler(context, history).Handle(
            new AddTaskPhaseCommand(taskId, "Segunda"), CancellationToken.None);

        second.Position.Should().Be(1);
        second.IsCurrent.Should().BeFalse();
        history.Records.Should().ContainSingle();
        history.Records[0].Action.Should().Be(TaskHistoryAction.PhaseAdded);
    }

    [Fact]
    public async Task Add_InvalidName_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);

        var emptyAction = () => AddHandler(context, new RecordingTaskHistoryWriter())
            .Handle(new AddTaskPhaseCommand(taskId, "  "), CancellationToken.None);
        (await emptyAction.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O nome da fase é obrigatório.*");

        var longAction = () => AddHandler(context, new RecordingTaskHistoryWriter())
            .Handle(
                new AddTaskPhaseCommand(taskId, new string('f', 121)),
                CancellationToken.None);
        (await longAction.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*no máximo 120 caracteres*");
    }

    // ── Update ────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ShouldRenameAndRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var phaseId = await SeedPhaseAsync(context, taskId, "Antes", 0);
        var history = new RecordingTaskHistoryWriter();

        var dto = await UpdateHandler(context, history).Handle(
            new UpdateTaskPhaseCommand(phaseId, Name: "Depois"),
            CancellationToken.None);

        dto.Name.Should().Be("Depois");
        history.Records.Should().ContainSingle();
        history.Records[0].Action.Should().Be(TaskHistoryAction.PhaseUpdated);
        history.Records[0].Detail.Should().Be("nome: Antes → Depois");
    }

    [Fact]
    public async Task Update_SetCurrent_ShouldClearSiblingPointer()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var first = await SeedPhaseAsync(context, taskId, "Primeira", 0);
        await UpdateHandler(context, new RecordingTaskHistoryWriter()).Handle(
            new UpdateTaskPhaseCommand(first, Current: true), CancellationToken.None);
        var second = await SeedPhaseAsync(context, taskId, "Segunda", 1);
        var history = new RecordingTaskHistoryWriter();

        var dto = await UpdateHandler(context, history).Handle(
            new UpdateTaskPhaseCommand(second, Current: true),
            CancellationToken.None);

        dto.IsCurrent.Should().BeTrue();
        var firstAfter = await context.TaskPhases.AsNoTracking()
            .SingleAsync(phase => phase.Id == first);
        firstAfter.IsCurrent.Should().BeFalse(); // ponteiro único

        history.Records.Should().ContainSingle();
        history.Records[0].Action.Should().Be(TaskHistoryAction.PhaseCurrentChanged);
        history.Records[0].Detail.Should().Be("Segunda");
    }

    [Fact]
    public async Task Update_CompleteAndReopen_ShouldStampAndRecordEach()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var phaseId = await SeedPhaseAsync(context, taskId, "Execução", 0);
        var history = new RecordingTaskHistoryWriter();

        var completed = await UpdateHandler(context, history).Handle(
            new UpdateTaskPhaseCommand(phaseId, Completed: true),
            CancellationToken.None);
        completed.CompletedAt.Should().NotBeNull();
        history.Records.Should().ContainSingle();
        history.Records[0].Action.Should().Be(TaskHistoryAction.PhaseCompleted);
        history.Records[0].Detail.Should().Be("Execução");

        history.Records.Clear();
        var reopened = await UpdateHandler(context, history).Handle(
            new UpdateTaskPhaseCommand(phaseId, Completed: false),
            CancellationToken.None);
        reopened.CompletedAt.Should().BeNull();
        history.Records.Should().ContainSingle();
        history.Records[0].Action.Should().Be(TaskHistoryAction.PhaseReopened);
    }

    [Fact]
    public async Task Update_MultipleAspects_ShouldRecordOneEntryPerAspect()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var phaseId = await SeedPhaseAsync(context, taskId, "Velho", 0);
        var history = new RecordingTaskHistoryWriter();

        await UpdateHandler(context, history).Handle(
            new UpdateTaskPhaseCommand(phaseId, Name: "Novo", Completed: true),
            CancellationToken.None);

        history.Records.Should().HaveCount(2);
        history.Records[0].Action.Should().Be(TaskHistoryAction.PhaseUpdated);
        history.Records[1].Action.Should().Be(TaskHistoryAction.PhaseCompleted);
        history.Records.Should().OnlyContain(record => record.TaskId == taskId);
    }

    [Fact]
    public async Task Update_NoOp_ShouldNotRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var phaseId = await SeedPhaseAsync(context, taskId, "Estável", 0);
        var history = new RecordingTaskHistoryWriter();

        // Mesmo nome; current/completed não vieram (null = mantém).
        var dto = await UpdateHandler(context, history).Handle(
            new UpdateTaskPhaseCommand(phaseId, Name: "Estável"),
            CancellationToken.None);

        dto.Name.Should().Be("Estável");
        history.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_UnknownPhase_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => UpdateHandler(context, new RecordingTaskHistoryWriter())
            .Handle(
                new UpdateTaskPhaseCommand(unknown, Completed: true),
                CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Fase '{unknown}' não encontrada.");
    }

    // ── Delete ────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ShouldRemoveAndRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var phaseId = await SeedPhaseAsync(context, taskId, "Descartar", 0);
        var history = new RecordingTaskHistoryWriter();

        await DeleteHandler(context, history).Handle(
            new DeleteTaskPhaseCommand(phaseId), CancellationToken.None);

        (await context.TaskPhases.AsNoTracking()
            .CountAsync(phase => phase.Id == phaseId))
            .Should().Be(0);
        history.Records.Should().ContainSingle();
        history.Records[0].Action.Should().Be(TaskHistoryAction.PhaseRemoved);
        history.Records[0].Detail.Should().Be("Descartar");
    }

    [Fact]
    public async Task Delete_UnknownPhase_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => DeleteHandler(context, new RecordingTaskHistoryWriter())
            .Handle(new DeleteTaskPhaseCommand(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Fase '{unknown}' não encontrada.");
    }

    // ── Query ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Query_ShouldOrderByPositionWithGapsAfterDelete()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var first = await SeedPhaseAsync(context, taskId, "Primeira", 0);
        var second = await SeedPhaseAsync(context, taskId, "Segunda", 1);
        await SeedPhaseAsync(context, taskId, "Terceira", 2);

        // Exclui a do meio: a ordenação por posição aceita o gap (1 vaza).
        context.TaskPhases.Remove(
            await context.TaskPhases.SingleAsync(phase => phase.Id == second));
        await context.SaveChangesAsync();

        var rows = await QueryHandler(context).Handle(
            new GetTaskPhasesQuery(taskId), CancellationToken.None);

        rows.Select(phase => phase.Position).Should().ContainInOrder(0, 2);
        rows.Select(phase => phase.Id).Should().Contain(first);
        rows.Should().OnlyContain(phase => phase.TaskId == taskId);
    }

    [Fact]
    public async Task Query_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => QueryHandler(context).Handle(
            new GetTaskPhasesQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }
}

using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Tasks.Commands;
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
/// F23-T003: planejamento, resultado (§5) e replanejamento (§13) da Task
/// — substituição com nulls limpando, guards pt-BR (400 fim &lt; início,
/// 400 motivo vazio/sem mudanças, 404 task), histórico "de → para" com
/// no-op sem registro e UMA entrada Replanned com o motivo (≤200).
/// </summary>
public sealed class TaskPlanningCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskPlanningCommandTests()
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
    private static TaskIncidentRepository IncidentRepo(AppDbContext context) => new(context);

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId;
        public string? UserName => "gestor@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => ["Admin"];
    }

    private static UpdateTaskPlanningCommandHandler PlanningHandler(
        AppDbContext context, RecordingTaskHistoryWriter history, Guid actorId)
        => new(
            TaskRepo(context),
            AssigneeRepo(context),
            DeviceRepo(context),
            IncidentRepo(context),
            history,
            new FakeCurrentUser(actorId),
            NullLogger<UpdateTaskPlanningCommandHandler>.Instance);

    private static UpdateTaskResultCommandHandler ResultHandler(
        AppDbContext context, RecordingTaskHistoryWriter history, Guid actorId)
        => new(
            TaskRepo(context),
            AssigneeRepo(context),
            DeviceRepo(context),
            IncidentRepo(context),
            history,
            new FakeCurrentUser(actorId),
            NullLogger<UpdateTaskResultCommandHandler>.Instance);

    private static ReplanTaskCommandHandler ReplanHandler(
        AppDbContext context, RecordingTaskHistoryWriter history, Guid actorId)
        => new(
            TaskRepo(context),
            AssigneeRepo(context),
            DeviceRepo(context),
            IncidentRepo(context),
            history,
            new FakeCurrentUser(actorId),
            NullLogger<ReplanTaskCommandHandler>.Instance);

    private static async Task<Guid> SeedAsync(AppDbContext context)
    {
        context.Tasks.Add(TaskItem.Create("Task com planejamento", TaskKind.Project));
        await context.SaveChangesAsync();
        return (await context.Tasks.AsNoTracking().SingleAsync()).Id;
    }

    // ── Comandos ───────────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireOperate()
    {
        new UpdateTaskPlanningCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new UpdateTaskResultCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new ReplanTaskCommand(Guid.NewGuid(), Reason: "ajuste").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
    }

    [Fact]
    public async Task SetPlanning_ShouldPersistDates()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedAsync(context);
        var start = new DateOnly(2026, 11, 1);
        var end = new DateOnly(2026, 11, 30);

        var dto = await PlanningHandler(context, new RecordingTaskHistoryWriter(), Guid.NewGuid())
            .Handle(
                new UpdateTaskPlanningCommand(taskId, start, end),
                CancellationToken.None);

        dto.PlannedStart.Should().Be(start);
        dto.PlannedEnd.Should().Be(end);

        var saved = await context.Tasks.AsNoTracking().SingleAsync();
        saved.PlannedStart.Should().Be(start);
        saved.PlannedEnd.Should().Be(end);
    }

    [Fact]
    public async Task SetPlanning_EndBeforeStart_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedAsync(context);

        var action = () => PlanningHandler(
                context, new RecordingTaskHistoryWriter(), Guid.NewGuid())
            .Handle(
                new UpdateTaskPlanningCommand(
                    taskId,
                    new DateOnly(2026, 11, 30),
                    new DateOnly(2026, 11, 1)),
                CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O fim planejado não pode ser anterior ao início planejado.*");
    }

    [Fact]
    public async Task SetPlanning_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => PlanningHandler(
                context, new RecordingTaskHistoryWriter(), Guid.NewGuid())
            .Handle(new UpdateTaskPlanningCommand(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task SetPlanning_ShouldRecordOneHistoryEntryPerChangedField()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedAsync(context);
        var history = new RecordingTaskHistoryWriter();
        var handler = PlanningHandler(context, history, Guid.NewGuid());
        var start = new DateOnly(2026, 11, 1);
        var end = new DateOnly(2026, 11, 30);

        // 1ª gravação: as duas datas nulas → 2 registros.
        await handler.Handle(
            new UpdateTaskPlanningCommand(taskId, start, end),
            CancellationToken.None);

        history.Records.Select(record => record.Action)
            .Should().Equal(
                TaskHistoryAction.PlanningChanged,
                TaskHistoryAction.PlanningChanged);
        history.Records[0].Detail.Should().Be("início sem início → 2026-11-01");
        history.Records[1].Detail.Should().Be("fim sem fim → 2026-11-30");

        // Mesmos valores = no-op: nenhum registro novo.
        await handler.Handle(
            new UpdateTaskPlanningCommand(taskId, start, end),
            CancellationToken.None);
        history.Records.Should().HaveCount(2);

        // Só o fim muda (limpeza) → 1 registro, formato "de → para".
        await handler.Handle(
            new UpdateTaskPlanningCommand(taskId, start),
            CancellationToken.None);
        history.Records.Should().HaveCount(3);
        history.Records[2].Action.Should().Be(TaskHistoryAction.PlanningChanged);
        history.Records[2].Detail.Should().Be("fim 2026-11-30 → sem fim");
        history.Records.Should().OnlyContain(record => record.TaskId == taskId);
    }

    [Fact]
    public async Task SetResult_ShouldPersistAndRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedAsync(context);
        var history = new RecordingTaskHistoryWriter();
        var handler = ResultHandler(context, history, Guid.NewGuid());

        // 1ª gravação: sem resultado → texto.
        var dto = await handler.Handle(
            new UpdateTaskResultCommand(taskId, "Migração concluída sem downtime."),
            CancellationToken.None);

        dto.Result.Should().Be("Migração concluída sem downtime.");
        history.Records.Should().ContainSingle();
        history.Records[0].Action.Should().Be(TaskHistoryAction.ResultChanged);
        history.Records[0].Detail.Should().Be(
            "sem resultado → Migração concluída sem downtime.");

        // Mesmo texto = no-op.
        await handler.Handle(
            new UpdateTaskResultCommand(taskId, "Migração concluída sem downtime."),
            CancellationToken.None);
        history.Records.Should().HaveCount(1);

        // Troca de texto → "de → para"; limpeza → "… → removido".
        await handler.Handle(
            new UpdateTaskResultCommand(taskId, "Revertida por impedimento."),
            CancellationToken.None);
        await handler.Handle(
            new UpdateTaskResultCommand(taskId), CancellationToken.None);

        history.Records.Should().HaveCount(3);
        history.Records[1].Detail.Should().Be(
            "Migração concluída sem downtime. → Revertida por impedimento.");
        history.Records[2].Detail.Should().Be("Revertida por impedimento. → removido");

        var saved = await context.Tasks.AsNoTracking().SingleAsync();
        saved.Result.Should().BeNull();
    }

    [Fact]
    public async Task SetResult_OverLimit_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedAsync(context);

        var action = () => ResultHandler(
                context, new RecordingTaskHistoryWriter(), Guid.NewGuid())
            .Handle(
                new UpdateTaskResultCommand(taskId, new string('r', 2001)),
                CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O resultado pode ter no máximo 2000 caracteres.*");
    }

    [Fact]
    public async Task Replan_ShouldApplyFieldsAndRecordSingleEntryWithReason()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedAsync(context);
        var history = new RecordingTaskHistoryWriter();
        var handler = ReplanHandler(context, history, Guid.NewGuid());
        var start = new DateOnly(2026, 12, 1);
        var end = new DateOnly(2026, 12, 20);
        var due = new DateOnly(2026, 12, 31);

        var dto = await handler.Handle(
            new ReplanTaskCommand(
                taskId, start, end, due, "Aprovação do cliente atrasou o início."),
            CancellationToken.None);

        dto.PlannedStart.Should().Be(start);
        dto.PlannedEnd.Should().Be(end);
        dto.DueDate.Should().Be(due);

        // UMA entrada com todos os campos alterados + motivo, ≤200.
        history.Records.Should().ContainSingle();
        var record = history.Records[0];
        record.Action.Should().Be(TaskHistoryAction.Replanned);
        record.Detail.Should().Contain("início sem início → 2026-12-01");
        record.Detail.Should().Contain("fim sem fim → 2026-12-20");
        record.Detail.Should().Contain("prazo sem prazo → 2026-12-31");
        record.Detail.Should().Contain(
            "— motivo: Aprovação do cliente atrasou o início.");
        record.Detail!.Length.Should().BeLessThanOrEqualTo(
            Domain.Entities.TaskHistory.DetailMaxLength);

        var saved = await context.Tasks.AsNoTracking().SingleAsync();
        saved.DueDate.Should().Be(due);
    }

    [Fact]
    public async Task Replan_WithoutReason_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedAsync(context);

        var action = () => ReplanHandler(
                context, new RecordingTaskHistoryWriter(), Guid.NewGuid())
            .Handle(
                new ReplanTaskCommand(taskId, new DateOnly(2026, 12, 1)),
                CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O motivo do replanejamento é obrigatório.*");
    }

    [Fact]
    public async Task Replan_ReasonAboveLimit_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedAsync(context);

        var action = () => ReplanHandler(
                context, new RecordingTaskHistoryWriter(), Guid.NewGuid())
            .Handle(
                new ReplanTaskCommand(
                    taskId, new DateOnly(2026, 12, 1),
                    Reason: new string('m', ReplanTaskCommand.ReasonMaxLength + 1)),
                CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*máximo 80 caracteres*");
    }

    [Fact]
    public async Task Replan_WithoutChanges_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedAsync(context);

        var action = () => ReplanHandler(
                context, new RecordingTaskHistoryWriter(), Guid.NewGuid())
            .Handle(
                new ReplanTaskCommand(taskId, Reason: "nada mudou de verdade"),
                CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Nada a replanejar:*");
    }

    [Fact]
    public async Task Replan_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => ReplanHandler(
                context, new RecordingTaskHistoryWriter(), Guid.NewGuid())
            .Handle(
                new ReplanTaskCommand(unknown, Reason: "motivo"),
                CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }
}

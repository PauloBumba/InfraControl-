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
/// F23-T003: critérios de aceite (§12) — CRUD no molde dos comentários:
/// 404 pt-BR (task/critério), 400 do domínio (descrição vazia/300),
/// histórico CriterionAdded/Updated/Removed com detail ≤200 e no-op
/// sem registro.
/// </summary>
public sealed class TaskCriterionCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskCriterionCommandTests()
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
    private static TaskCriterionRepository CriterionRepo(AppDbContext context) => new(context);

    private static AddTaskCriterionCommandHandler AddHandler(
        AppDbContext context, RecordingTaskHistoryWriter history)
        => new(
            TaskRepo(context),
            CriterionRepo(context),
            history,
            NullLogger<AddTaskCriterionCommandHandler>.Instance);

    private static UpdateTaskCriterionCommandHandler UpdateHandler(
        AppDbContext context, RecordingTaskHistoryWriter history)
        => new(
            CriterionRepo(context),
            history,
            NullLogger<UpdateTaskCriterionCommandHandler>.Instance);

    private static DeleteTaskCriterionCommandHandler DeleteHandler(
        AppDbContext context, RecordingTaskHistoryWriter history)
        => new(
            CriterionRepo(context),
            history,
            NullLogger<DeleteTaskCriterionCommandHandler>.Instance);

    private static GetTaskCriteriaQueryHandler QueryHandler(AppDbContext context)
        => new(TaskRepo(context), CriterionRepo(context));

    private static async Task<Guid> SeedTaskAsync(AppDbContext context)
    {
        context.Tasks.Add(TaskItem.Create("Task com critérios", TaskKind.Manual));
        await context.SaveChangesAsync();
        return (await context.Tasks.AsNoTracking().SingleAsync()).Id;
    }

    // ── Comandos ───────────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireOperate()
    {
        new AddTaskCriterionCommand(Guid.NewGuid(), "ok").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new UpdateTaskCriterionCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new DeleteTaskCriterionCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new GetTaskCriteriaQuery(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Read);
    }

    [Fact]
    public async Task Add_ShouldPersistCriterionAndRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var history = new RecordingTaskHistoryWriter();

        var dto = await AddHandler(context, history).Handle(
            new AddTaskCriterionCommand(taskId, "  plano aprovado pelo comitê  "),
            CancellationToken.None);

        dto.TaskId.Should().Be(taskId);
        dto.Description.Should().Be("plano aprovado pelo comitê");
        dto.IsDone.Should().BeFalse();
        dto.DoneAt.Should().BeNull();

        history.Records.Should().ContainSingle();
        history.Records[0].Action.Should().Be(TaskHistoryAction.CriterionAdded);
        history.Records[0].Detail.Should().Be("plano aprovado pelo comitê");
        history.Records[0].TaskId.Should().Be(taskId);

        var saved = await context.TaskCriteria.AsNoTracking().SingleAsync();
        saved.Description.Should().Be("plano aprovado pelo comitê");
    }

    [Fact]
    public async Task Add_LongDescription_ShouldTruncateHistoryDetail()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var history = new RecordingTaskHistoryWriter();
        var description = new string('c', 300);

        await AddHandler(context, history).Handle(
            new AddTaskCriterionCommand(taskId, description),
            CancellationToken.None);

        history.Records[0].Detail.Should().HaveLength(190);
        history.Records[0].Detail.Should().EndWith("…");
        history.Records[0].Detail!.Length.Should().BeLessThanOrEqualTo(
            TaskHistory.DetailMaxLength);
    }

    [Fact]
    public async Task Add_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => AddHandler(context, new RecordingTaskHistoryWriter())
            .Handle(
                new AddTaskCriterionCommand(unknown, "ok"),
                CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Add_BlankDescription_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);

        var action = () => AddHandler(context, new RecordingTaskHistoryWriter())
            .Handle(
                new AddTaskCriterionCommand(taskId, "   "),
                CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("A descrição do critério é obrigatória.*");
    }

    [Fact]
    public async Task Add_DescriptionAbove300_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);

        var action = () => AddHandler(context, new RecordingTaskHistoryWriter())
            .Handle(
                new AddTaskCriterionCommand(taskId, new string('c', 301)),
                CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*máximo 300 caracteres*");
    }

    // ── Atualização ────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ToggleDone_ShouldRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var history = new RecordingTaskHistoryWriter();
        var added = await AddHandler(context, history).Handle(
            new AddTaskCriterionCommand(taskId, "teste automatizado verde"),
            CancellationToken.None);
        history.Records.Clear();

        // Marcar como atendido → carimbo + histórico.
        var done = await UpdateHandler(context, history).Handle(
            new UpdateTaskCriterionCommand(added.Id, Done: true),
            CancellationToken.None);

        done.IsDone.Should().BeTrue();
        done.DoneAt.Should().NotBeNull();
        history.Records.Should().ContainSingle();
        history.Records[0].Action.Should().Be(TaskHistoryAction.CriterionUpdated);
        history.Records[0].Detail.Should().Be(
            "marcado como atendido: teste automatizado verde");

        // Desmarcar → carimbo limpo + histórico.
        var undone = await UpdateHandler(context, history).Handle(
            new UpdateTaskCriterionCommand(added.Id, Done: false),
            CancellationToken.None);

        undone.IsDone.Should().BeFalse();
        undone.DoneAt.Should().BeNull();
        history.Records.Should().HaveCount(2);
        history.Records[1].Detail.Should().Be(
            "desmarcado: teste automatizado verde");
    }

    [Fact]
    public async Task Update_Description_ShouldRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var history = new RecordingTaskHistoryWriter();
        var added = await AddHandler(context, history).Handle(
            new AddTaskCriterionCommand(taskId, "versão antiga"),
            CancellationToken.None);
        history.Records.Clear();

        await UpdateHandler(context, history).Handle(
            new UpdateTaskCriterionCommand(added.Id, Description: "versão nova"),
            CancellationToken.None);

        history.Records.Should().ContainSingle();
        history.Records[0].Action.Should().Be(TaskHistoryAction.CriterionUpdated);
        history.Records[0].Detail.Should().Be(
            "descrição: versão antiga → versão nova");
    }

    [Fact]
    public async Task Update_NoChanges_ShouldNotRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var history = new RecordingTaskHistoryWriter();
        var added = await AddHandler(context, history).Handle(
            new AddTaskCriterionCommand(taskId, "sem mudança"),
            CancellationToken.None);
        history.Records.Clear();

        var dto = await UpdateHandler(context, history).Handle(
            new UpdateTaskCriterionCommand(added.Id),
            CancellationToken.None);

        dto.Description.Should().Be("sem mudança");
        history.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_UnknownCriterion_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => UpdateHandler(context, new RecordingTaskHistoryWriter())
            .Handle(
                new UpdateTaskCriterionCommand(unknown, Done: true),
                CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Critério '{unknown}' não encontrado.");
    }

    // ── Exclusão ───────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ShouldRemoveAndRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var history = new RecordingTaskHistoryWriter();
        var added = await AddHandler(context, history).Handle(
            new AddTaskCriterionCommand(taskId, "critério descartado"),
            CancellationToken.None);
        history.Records.Clear();

        await DeleteHandler(context, history).Handle(
            new DeleteTaskCriterionCommand(added.Id),
            CancellationToken.None);

        (await context.TaskCriteria.AsNoTracking().ToListAsync())
            .Should().BeEmpty();
        history.Records.Should().ContainSingle();
        history.Records[0].Action.Should().Be(TaskHistoryAction.CriterionRemoved);
        history.Records[0].Detail.Should().Be("critério descartado");
    }

    [Fact]
    public async Task Delete_UnknownCriterion_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => DeleteHandler(context, new RecordingTaskHistoryWriter())
            .Handle(new DeleteTaskCriterionCommand(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Critério '{unknown}' não encontrado.");
    }

    // ── Consulta ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetCriteria_ShouldReturnChronological()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context);
        var history = new RecordingTaskHistoryWriter();
        await AddHandler(context, history).Handle(
            new AddTaskCriterionCommand(taskId, "primeiro"),
            CancellationToken.None);
        await AddHandler(context, history).Handle(
            new AddTaskCriterionCommand(taskId, "segundo"),
            CancellationToken.None);

        var rows = await QueryHandler(context).Handle(
            new GetTaskCriteriaQuery(taskId), CancellationToken.None);

        rows.Select(row => row.Description).Should().Equal("primeiro", "segundo");
        rows.Should().OnlyContain(row => row.TaskId == taskId);
    }

    [Fact]
    public async Task GetCriteria_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => QueryHandler(context).Handle(
            new GetTaskCriteriaQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }
}

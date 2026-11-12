using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.History;
using InfraControl.Application.Tasks.History.Ports;
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
/// F7-T007: ChangeTaskStateCommand — transição válida muda o estado e grava
/// `StateChanged` no histórico; 400 (estado inválido), 404 (task) e 409
/// (transição fora do mapa) em pt-BR.
/// </summary>
public sealed class TaskStateCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskStateCommandTests()
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

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId;
        public string? UserName => "gestor@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => ["Admin"];
    }

    private static async Task<Guid> SeedTaskAsync(AppDbContext context, TaskState state)
    {
        var task = TaskItem.Create("Task workflow comando", TaskKind.Manual);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        if (state != TaskState.Open)
            task.ChangeState(state);
        await context.SaveChangesAsync();
        return task.Id;
    }

    private static ChangeTaskStateCommandHandler Handler(
        AppDbContext context, RecordingTaskHistoryWriter history)
        => new(
            new TaskItemRepository(context),
            history,
            new TaskAssigneeRepository(context),
            new TaskDeviceRepository(context),
            new TaskIncidentRepository(context),
            new FakeCurrentUser(Guid.NewGuid()),
            NullLogger<ChangeTaskStateCommandHandler>.Instance);

    [Fact]
    public async Task ValidTransition_ShouldChangeStateAndRecordStateChangedHistory()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context, TaskState.Open);
        var history = new RecordingTaskHistoryWriter();

        var dto = await Handler(context, history)
            .Handle(new ChangeTaskStateCommand(taskId, "InProgress"), default);

        dto.State.Should().Be(nameof(TaskState.InProgress));
        var entry = history.Records.Single();
        entry.Action.Should().Be(TaskHistoryAction.StateChanged);
        entry.Detail.Should().Be("Open → InProgress");
        entry.TaskId.Should().Be(taskId);
    }

    [Fact]
    public async Task InvalidTransition_ShouldThrowInvalidOperationExceptionPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context, TaskState.Open);
        var history = new RecordingTaskHistoryWriter();

        var act = () => Handler(context, history)
            .Handle(new ChangeTaskStateCommand(taskId, "Closed"), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Transição inválida: 'Open' → 'Closed'.");
        history.Records.Should().BeEmpty("transição rejeitada não gera histórico");
    }

    [Fact]
    public async Task UnknownTask_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var history = new RecordingTaskHistoryWriter();

        var act = () => Handler(context, history)
            .Handle(new ChangeTaskStateCommand(Guid.NewGuid(), "InProgress"), default);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Task '*' não encontrada.");
    }

    [Fact]
    public async Task UnknownStateValue_ShouldThrowArgumentExceptionPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context, TaskState.Open);
        var history = new RecordingTaskHistoryWriter();

        var act = () => Handler(context, history)
            .Handle(new ChangeTaskStateCommand(taskId, "EmAndamento"), default);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Estado inválido*");
    }

    [Fact]
    public async Task EmptyState_ShouldThrowArgumentExceptionPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedTaskAsync(context, TaskState.Open);
        var history = new RecordingTaskHistoryWriter();

        var act = () => Handler(context, history)
            .Handle(new ChangeTaskStateCommand(taskId, "  "), default);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*obrigatório*");
    }

    // ── F52-T002 — baixa justificada (padrão ServiceNow) ──────────────

    private static async Task<Guid> SeedInProgressAsync(
        AppDbContext context, string? result = "Execução concluída sem imprevisto.")
    {
        var task = TaskItem.Create("Task a finalizar", TaskKind.Manual);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        task.ChangeState(TaskState.InProgress);
        task.SetResult(result);
        await context.SaveChangesAsync();
        return task.Id;
    }

    private static async Task<Guid> SeedResolvedAsync(AppDbContext context)
    {
        var id = await SeedInProgressAsync(context);
        var task = await context.Tasks.SingleAsync(t => t.Id == id);
        task.ChangeState(TaskState.Resolved);
        task.SetResolutionCode(ResolutionCodes.Parse("Fixed"));
        await context.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Resolve_WithResultAndCode_ShouldChangeStateAndKeepCode()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedInProgressAsync(context);
        var history = new RecordingTaskHistoryWriter();

        var dto = await Handler(context, history)
            .Handle(new ChangeTaskStateCommand(taskId, "Resolved", "Fixed"), default);

        dto.State.Should().Be(nameof(TaskState.Resolved));
        dto.ResolutionCode.Should().Be("Fixed");
        var reloaded = await context.Tasks.SingleAsync(t => t.Id == taskId);
        reloaded.ResolutionCode.Should().Be(ResolutionCode.Fixed);
    }

    [Fact]
    public async Task Resolve_WithoutResult_ShouldThrowPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedInProgressAsync(context, result: null);
        var history = new RecordingTaskHistoryWriter();

        var act = () => Handler(context, history)
            .Handle(new ChangeTaskStateCommand(taskId, "Resolved", "Fixed"), default);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Informe o que foi feito para finalizar a tarefa.*");
        history.Records.Should().BeEmpty("conclusão rejeitada não gera histórico");
    }

    [Fact]
    public async Task Resolve_WithoutCode_ShouldThrowPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedInProgressAsync(context);
        var history = new RecordingTaskHistoryWriter();

        var act = () => Handler(context, history)
            .Handle(new ChangeTaskStateCommand(taskId, "Resolved"), default);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Código de baixa inválido*");
    }

    [Fact]
    public async Task Resolve_WithInvalidCode_ShouldEchoItPtBr()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedInProgressAsync(context);
        var history = new RecordingTaskHistoryWriter();

        var act = () => Handler(context, history)
            .Handle(new ChangeTaskStateCommand(taskId, "Resolved", "banana"), default);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*banana*");
    }

    [Fact]
    public async Task Reopen_ShouldClearResolutionCode()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedResolvedAsync(context);
        var history = new RecordingTaskHistoryWriter();

        var dto = await Handler(context, history)
            .Handle(new ChangeTaskStateCommand(taskId, "InProgress"), default);

        dto.State.Should().Be(nameof(TaskState.InProgress));
        dto.ResolutionCode.Should().BeNull("sair do estado conclusivo limpa o código");
        var reloaded = await context.Tasks.SingleAsync(t => t.Id == taskId);
        reloaded.ResolutionCode.Should().BeNull();
    }

    [Fact]
    public async Task Close_FromResolved_WithNewCode_ShouldSucceed()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedResolvedAsync(context);
        var history = new RecordingTaskHistoryWriter();

        var dto = await Handler(context, history)
            .Handle(new ChangeTaskStateCommand(taskId, "Closed", "Workaround"), default);

        dto.State.Should().Be(nameof(TaskState.Closed));
        dto.ResolutionCode.Should().Be("Workaround");
    }

    [Fact]
    public async Task Cancelled_ShouldNotRequireResultNorCode()
    {
        await using var context = await CreateContextAsync();
        var taskId = await SeedInProgressAsync(context, result: null);
        var history = new RecordingTaskHistoryWriter();

        var dto = await Handler(context, history)
            .Handle(new ChangeTaskStateCommand(taskId, "Cancelled"), default);

        dto.State.Should().Be(nameof(TaskState.Cancelled));
        dto.ResolutionCode.Should().BeNull();
    }
}

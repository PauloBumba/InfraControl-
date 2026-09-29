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
}

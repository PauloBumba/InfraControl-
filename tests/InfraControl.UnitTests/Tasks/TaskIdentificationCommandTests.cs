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
/// F23-T001: identificação básica da Task (prioridade/prazo/responsável)
/// — substituição dos 3 campos, guards pt-BR (400 prioridade, 404 task/
///usuário) e um registro de histórico por campo alterado.
/// </summary>
public sealed class TaskIdentificationCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskIdentificationCommandTests()
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

    private static SetTaskIdentificationCommandHandler Handler(
        AppDbContext context, RecordingTaskHistoryWriter history, Guid actorId)
        => new(
            TaskRepo(context),
            AssigneeRepo(context),
            DeviceRepo(context),
            IncidentRepo(context),
            history,
            new FakeCurrentUser(actorId),
            NullLogger<SetTaskIdentificationCommandHandler>.Instance);

    private static async Task<(Guid TaskId, Guid ResponsibleId)> SeedAsync(
        AppDbContext context)
    {
        var actor = User.Create("gestor@exemplo.test", "Gestor Tarefa", UserRole.Admin);
        var responsible = User.Create(
            "responsavel@exemplo.test", "Responsável Teste", UserRole.Viewer);
        context.DomainUsers.AddRange(actor, responsible);
        context.Tasks.Add(TaskItem.Create("Task com identificação", TaskKind.Project));
        await context.SaveChangesAsync();

        var task = await context.Tasks.AsNoTracking().SingleAsync();
        return (task.Id, responsible.Id);
    }

    // ── Comando ─────────────────────────────────────────────────────────

    [Fact]
    public void Command_ShouldRequireOperate()
        => new SetTaskIdentificationCommand(Guid.NewGuid(), "High").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);

    [Fact]
    public async Task Set_ShouldPersistIdentificationAndResolveResponsibleName()
    {
        await using var context = await CreateContextAsync();
        var (taskId, responsibleId) = await SeedAsync(context);
        var due = new DateOnly(2026, 10, 20);

        var dto = await Handler(context, new RecordingTaskHistoryWriter(), Guid.NewGuid())
            .Handle(
                new SetTaskIdentificationCommand(taskId, "High", due, responsibleId),
                CancellationToken.None);

        dto.Priority.Should().Be("High");
        dto.DueDate.Should().Be(due);
        dto.ResponsibleId.Should().Be(responsibleId);
        dto.ResponsibleName.Should().Be("Responsável Teste");

        var saved = await context.Tasks.AsNoTracking().SingleAsync();
        saved.Priority.Should().Be(TaskPriority.High);
        saved.DueDate.Should().Be(due);
        saved.ResponsibleId.Should().Be(responsibleId);
    }

    [Fact]
    public async Task Set_ShouldReplaceAndClearOptionalFields()
    {
        await using var context = await CreateContextAsync();
        var (taskId, responsibleId) = await SeedAsync(context);
        var history = new RecordingTaskHistoryWriter();
        var handler = Handler(context, history, Guid.NewGuid());

        await handler.Handle(
            new SetTaskIdentificationCommand(
                taskId, "Critical", new DateOnly(2026, 11, 2), responsibleId),
            CancellationToken.None);

        // Substituição: null limpa prazo e responsável; prioridade é enviada.
        var dto = await handler.Handle(
            new SetTaskIdentificationCommand(taskId, "Low"),
            CancellationToken.None);

        dto.Priority.Should().Be("Low");
        dto.DueDate.Should().BeNull();
        dto.ResponsibleId.Should().BeNull();
        dto.ResponsibleName.Should().BeNull();

        var saved = await context.Tasks.AsNoTracking().SingleAsync();
        saved.DueDate.Should().BeNull();
        saved.ResponsibleId.Should().BeNull();
    }

    [Fact]
    public async Task Set_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => Handler(context, new RecordingTaskHistoryWriter(), Guid.NewGuid())
            .Handle(new SetTaskIdentificationCommand(unknown, "High"),
                CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Set_WithoutPriority_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedAsync(context);

        var action = () => Handler(context, new RecordingTaskHistoryWriter(), Guid.NewGuid())
            .Handle(new SetTaskIdentificationCommand(taskId, "   "),
                CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("A prioridade é obrigatória.*");
    }

    [Fact]
    public async Task Set_UnknownPriority_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedAsync(context);

        var action = () => Handler(context, new RecordingTaskHistoryWriter(), Guid.NewGuid())
            .Handle(new SetTaskIdentificationCommand(taskId, "besouro"),
                CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Prioridade inválida: 'besouro'.*");
    }

    [Fact]
    public async Task Set_UnknownResponsible_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedAsync(context);
        var unknownUser = Guid.NewGuid();

        var action = () => Handler(context, new RecordingTaskHistoryWriter(), Guid.NewGuid())
            .Handle(new SetTaskIdentificationCommand(
                    taskId, "High", ResponsibleId: unknownUser),
                CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Usuário '{unknownUser}' não encontrado.");
    }

    // ── Histórico ───────────────────────────────────────────────────────

    [Fact]
    public async Task Set_ShouldRecordOneHistoryEntryPerChangedField()
    {
        await using var context = await CreateContextAsync();
        var (taskId, responsibleId) = await SeedAsync(context);
        var history = new RecordingTaskHistoryWriter();
        var handler = Handler(context, history, Guid.NewGuid());
        var due = new DateOnly(2026, 10, 20);

        // 1ª gravação: Medium→High, sem prazo→data, sem responsável→id.
        await handler.Handle(
            new SetTaskIdentificationCommand(taskId, "High", due, responsibleId),
            CancellationToken.None);

        history.Records.Select(record => record.Action)
            .Should().Equal(
                TaskHistoryAction.PriorityChanged,
                TaskHistoryAction.DueDateChanged,
                TaskHistoryAction.ResponsibleChanged);
        history.Records[0].Detail.Should().Be("Medium → High");
        history.Records[1].Detail.Should().Be("sem prazo → 2026-10-20");
        history.Records[2].Detail.Should().Be($"sem responsável → {responsibleId}");

        // Mesmos valores = no-op: nenhum registro novo.
        await handler.Handle(
            new SetTaskIdentificationCommand(taskId, "High", due, responsibleId),
            CancellationToken.None);
        history.Records.Should().HaveCount(3);

        // Só o prazo muda (limpeza) → 1 registro, formato "de → para".
        await handler.Handle(
            new SetTaskIdentificationCommand(taskId, "High", ResponsibleId: responsibleId),
            CancellationToken.None);
        history.Records.Should().HaveCount(4);
        history.Records[3].Action.Should().Be(TaskHistoryAction.DueDateChanged);
        history.Records[3].Detail.Should().Be("2026-10-20 → sem prazo");

        // Responsável removido → "… → removido".
        await handler.Handle(
            new SetTaskIdentificationCommand(taskId, "High"), CancellationToken.None);
        history.Records.Should().HaveCount(5);
        history.Records[4].Action.Should().Be(TaskHistoryAction.ResponsibleChanged);
        history.Records[4].Detail.Should().Be($"{responsibleId} → removido");
        history.Records.Should().OnlyContain(record => record.TaskId == taskId);
    }

    [Fact]
    public async Task Create_WithIdentification_ShouldPersistThroughCommand()
    {
        await using var context = await CreateContextAsync();
        var (_, responsibleId) = await SeedAsync(context);
        var history = new RecordingTaskHistoryWriter();
        var create = new CreateTaskCommandHandler(
            TaskRepo(context), AssigneeRepo(context), history,
            new FakeCurrentUser(Guid.NewGuid()),
            NullLogger<CreateTaskCommandHandler>.Instance);
        var due = new DateOnly(2026, 12, 1);

        var dto = await create.Handle(
            new CreateTaskCommand(
                "Sob demanda", "Project",
                Priority: "Critical", DueDate: due, ResponsibleId: responsibleId),
            CancellationToken.None);

        dto.Priority.Should().Be("Critical");
        dto.DueDate.Should().Be(due);
        dto.ResponsibleId.Should().Be(responsibleId);
        dto.ResponsibleName.Should().Be("Responsável Teste");

        var saved = await context.Tasks.AsNoTracking()
            .SingleAsync(task => task.Id == dto.Id);
        saved.Priority.Should().Be(TaskPriority.Critical);
    }

    [Fact]
    public async Task Create_UnknownResponsible_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknownUser = Guid.NewGuid();
        var create = new CreateTaskCommandHandler(
            TaskRepo(context), AssigneeRepo(context),
            new RecordingTaskHistoryWriter(),
            new FakeCurrentUser(Guid.NewGuid()),
            NullLogger<CreateTaskCommandHandler>.Instance);

        var action = () => create.Handle(
            new CreateTaskCommand("t", "Manual", ResponsibleId: unknownUser),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Usuário '{unknownUser}' não encontrado.");
    }
}

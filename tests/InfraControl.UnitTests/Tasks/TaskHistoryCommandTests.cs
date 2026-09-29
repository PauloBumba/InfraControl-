using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.History;
using InfraControl.Application.Tasks.History.Ports;
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
/// F7-T006: histórico — comandos gravam entradas com o actor correto,
/// leitura cronológica com nome do ator e 404 pt-BR para task inexistente.
/// </summary>
public sealed class TaskHistoryCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskHistoryCommandTests()
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
    private static IncidentRepository IncidentRepo(AppDbContext context) => new(context);
    private static TaskCommentRepository CommentRepo(AppDbContext context) => new(context);
    private static TaskHistoryRepository HistoryRepo(AppDbContext context) => new(context);

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId;
        public string? UserName => "ator@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => ["Admin"];
    }

    private static async Task<(Guid TaskId, Guid ActorId, Guid DeviceId)> SeedAsync(
        AppDbContext context)
    {
        var actor = User.Create("ator@exemplo.test", "Ator Histórico", UserRole.Admin);
        context.DomainUsers.Add(actor);
        await context.SaveChangesAsync();

        var location = Location.Create("Sala F7T006");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(
            location.Id, "sw-f7t006", DeviceType.Switch, ip: "192.0.2.74");
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var task = TaskItem.Create("Task com histórico", TaskKind.Manual);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return (task.Id, actor.Id, device.Id);
    }

    // ── Gravação pelos comandos ──────────────────────────────────────────

    [Fact]
    public async Task CreateUpdateAndAssign_ShouldRecordHistoryEntries()
    {
        await using var context = await CreateContextAsync();
        var (taskId, actorId, _) = await SeedAsync(context);
        var history = new RecordingTaskHistoryWriter();

        var create = new CreateTaskCommandHandler(
            TaskRepo(context), history,
            NullLogger<CreateTaskCommandHandler>.Instance);
        var dto = await create.Handle(
            new CreateTaskCommand("Segunda task", "Manual"),
            CancellationToken.None);

        var update = new UpdateTaskCommandHandler(
            TaskRepo(context), history,
            NullLogger<UpdateTaskCommandHandler>.Instance);
        await update.Handle(
            new UpdateTaskCommand(dto.Id, "Segunda task (rev.)"),
            CancellationToken.None);

        var assign = new AssignTaskCommandHandler(
            TaskRepo(context), AssigneeRepo(context), history,
            NullLogger<AssignTaskCommandHandler>.Instance);
        await assign.Handle(
            new AssignTaskCommand(dto.Id, actorId), CancellationToken.None);

        history.Records.Select(record => record.Action)
            .Should().Equal(
                TaskHistoryAction.Created,
                TaskHistoryAction.Updated,
                TaskHistoryAction.Assigned);
        history.Records[1].Detail.Should().Be("Segunda task (rev.)");
        history.Records[2].Detail.Should().Be(actorId.ToString());
        history.Records.Should().OnlyContain(record => record.TaskId == dto.Id);
    }

    [Fact]
    public async Task LinkAndComment_ShouldRecordHistoryEntries()
    {
        await using var context = await CreateContextAsync();
        var (taskId, actorId, deviceId) = await SeedAsync(context);
        var history = new RecordingTaskHistoryWriter();

        var link = new LinkTaskDeviceCommandHandler(
            TaskRepo(context), DeviceRepository(context),
            DeviceRepo(context), history,
            NullLogger<LinkTaskDeviceCommandHandler>.Instance);
        await link.Handle(
            new LinkTaskDeviceCommand(taskId, deviceId), CancellationToken.None);

        var comment = new AddTaskCommentCommandHandler(
            TaskRepo(context), CommentRepo(context), history,
            new FakeCurrentUser(actorId),
            NullLogger<AddTaskCommentCommandHandler>.Instance);
        await comment.Handle(
            new AddTaskCommentCommand(taskId, "feito"), CancellationToken.None);

        history.Records.Select(record => record.Action)
            .Should().Equal(
                TaskHistoryAction.DeviceLinked,
                TaskHistoryAction.Commented);
        history.Records[0].Detail.Should().Be(deviceId.ToString());
    }

    private static DeviceRepository DeviceRepository(AppDbContext context) => new(context);

    // ── Leitura ──────────────────────────────────────────────────────────

    [Fact]
    public void Query_ShouldRequireTasksRead()
        => new GetTaskHistoryQuery(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Read);

    [Fact]
    public async Task List_ShouldReturnChronologicalEntriesWithActorName()
    {
        await using var context = await CreateContextAsync();
        var (taskId, actorId, _) = await SeedAsync(context);
        var history = new RecordingTaskHistoryWriter();

        var create = new CreateTaskCommandHandler(
            TaskRepo(context), history,
            NullLogger<CreateTaskCommandHandler>.Instance);
        var dto = await create.Handle(
            new CreateTaskCommand("P/ histórico", "Manual"),
            CancellationToken.None);

        // Persiste entradas reais com actor (o writer fake não grava no banco).
        // Datas distintas via SQL: a auditoria sobrescreve CreateDateTime no
        // SaveChanges e as duas entradas nascem com o mesmo timestamp.
        var created = TaskHistory.Create(dto.Id, TaskHistoryAction.Created, actorId);
        var updated = TaskHistory.Create(
            dto.Id, TaskHistoryAction.Updated, actorId, "título novo");
        await HistoryRepo(context).AddAsync(created, CancellationToken.None);
        await HistoryRepo(context).AddAsync(updated, CancellationToken.None);
        await HistoryRepo(context).SaveChangesAsync(CancellationToken.None);

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE task_histories SET \"CreateDateTime\" = {0} WHERE id = {1}",
            new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc), created.Id);
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE task_histories SET \"CreateDateTime\" = {0} WHERE id = {1}",
            new DateTime(2026, 9, 27, 10, 1, 0, DateTimeKind.Utc), updated.Id);

        var handler = new GetTaskHistoryQueryHandler(
            HistoryRepo(context), TaskRepo(context));
        var entries = await handler.Handle(
            new GetTaskHistoryQuery(dto.Id), CancellationToken.None);

        entries.Select(entry => entry.Action)
            .Should().Equal("Created", "Updated");
        entries.Should().OnlyContain(entry => entry.ActorName == "Ator Histórico");
        entries.ElementAt(1).Detail.Should().Be("título novo");
    }

    [Fact]
    public async Task List_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var handler = new GetTaskHistoryQueryHandler(
            HistoryRepo(context), TaskRepo(context));
        var action = () => handler.Handle(
            new GetTaskHistoryQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }
}

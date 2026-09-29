using FluentAssertions;
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
/// F7-T002: atribuição de responsáveis — 404 pt-BR (task/usuário/vínculo),
/// 409 em duplicidade, TaskDto com assigneeIds e Delete com Cascade.
/// </summary>
public sealed class TaskAssignmentCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskAssignmentCommandTests()
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

    private static async Task<(Guid TaskId, Guid UserId)> SeedTaskAndUserAsync(
        AppDbContext context)
    {
        var task = TaskItem.Create("Tarefa", TaskKind.Manual);
        context.Tasks.Add(task);

        var user = User.Create(
            "ana@exemplo.test", "Ana Operadora", UserRole.Operator);
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();
        return (task.Id, user.Id);
    }

    private static RecordingTaskHistoryWriter History() => new();

    private static AssignTaskCommandHandler AssignHandler(AppDbContext context)
        => new(TaskRepo(context), AssigneeRepo(context), History(),
            NullLogger<AssignTaskCommandHandler>.Instance);

    private static UnassignTaskCommandHandler UnassignHandler(AppDbContext context)
        => new(TaskRepo(context), AssigneeRepo(context), History(),
            NullLogger<UnassignTaskCommandHandler>.Instance);

    private static GetTaskQueryHandler GetHandler(AppDbContext context)
        => new(TaskRepo(context), AssigneeRepo(context), DeviceRepo(context),
            IncidentRepo(context));

    private static TaskDeviceRepository DeviceRepo(AppDbContext context) => new(context);
    private static TaskIncidentRepository IncidentRepo(AppDbContext context) => new(context);

    // ── Assign ───────────────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireTasksManage()
    {
        new AssignTaskCommand(Guid.NewGuid(), Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new UnassignTaskCommand(Guid.NewGuid(), Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
    }

    [Fact]
    public async Task Assign_ShouldExposeAssigneeInTaskDto()
    {
        await using var context = await CreateContextAsync();
        var (taskId, userId) = await SeedTaskAndUserAsync(context);

        await AssignHandler(context).Handle(
            new AssignTaskCommand(taskId, userId), CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetTaskQuery(taskId), CancellationToken.None);
        dto.AssigneeCount.Should().Be(1);
        dto.AssigneeIds.Should().Equal(userId);
    }

    [Fact]
    public async Task Assign_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => AssignHandler(context).Handle(
            new AssignTaskCommand(unknown, Guid.NewGuid()), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Assign_UnknownUser_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedTaskAndUserAsync(context);
        var unknown = Guid.NewGuid();

        var action = () => AssignHandler(context).Handle(
            new AssignTaskCommand(taskId, unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Usuário '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Assign_Duplicate_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, userId) = await SeedTaskAndUserAsync(context);

        await AssignHandler(context).Handle(
            new AssignTaskCommand(taskId, userId), CancellationToken.None);

        var action = () => AssignHandler(context).Handle(
            new AssignTaskCommand(taskId, userId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("O usuário já está atribuído à task.");
    }

    // ── Unassign ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Unassign_ShouldRemoveLinkAndClearDto()
    {
        await using var context = await CreateContextAsync();
        var (taskId, userId) = await SeedTaskAndUserAsync(context);
        await AssignHandler(context).Handle(
            new AssignTaskCommand(taskId, userId), CancellationToken.None);

        await UnassignHandler(context).Handle(
            new UnassignTaskCommand(taskId, userId), CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetTaskQuery(taskId), CancellationToken.None);
        dto.AssigneeCount.Should().Be(0);
        dto.AssigneeIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Unassign_UnknownLink_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedTaskAndUserAsync(context);
        var stranger = Guid.NewGuid();

        var action = () => UnassignHandler(context).Handle(
            new UnassignTaskCommand(taskId, stranger), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Vínculo da task '{taskId}' com o usuário "
                + $"'{stranger}' não encontrado.");
    }

    // ── Integração com Delete / Search (F7-T001) ─────────────────────────

    [Fact]
    public async Task Delete_WithAssignee_ShouldCascadeAndRemoveTask()
    {
        await using var context = await CreateContextAsync();
        var (taskId, userId) = await SeedTaskAndUserAsync(context);
        await AssignHandler(context).Handle(
            new AssignTaskCommand(taskId, userId), CancellationToken.None);

        await new DeleteTaskCommandHandler(TaskRepo(context),
                NullLogger<DeleteTaskCommandHandler>.Instance)
            .Handle(new DeleteTaskCommand(taskId), CancellationToken.None);

        context.Tasks.Should().BeEmpty();
        context.TaskAssignees.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_ShouldEnrichPageWithAssigneeIds()
    {
        await using var context = await CreateContextAsync();
        var (taskId, userId) = await SeedTaskAndUserAsync(context);
        await AssignHandler(context).Handle(
            new AssignTaskCommand(taskId, userId), CancellationToken.None);

        var handler = new SearchTasksQueryHandler(
            TaskRepo(context), AssigneeRepo(context), DeviceRepo(context),
            IncidentRepo(context));
        var page = await handler.Handle(
            new SearchTasksQuery(), CancellationToken.None);

        var item = page.Items.Should().ContainSingle().Subject;
        item.AssigneeCount.Should().Be(1);
        item.AssigneeIds.Should().Equal(userId);
    }
}

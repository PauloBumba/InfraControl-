using FluentAssertions;
using InfraControl.Application.Inventory.Devices.Commands;
using InfraControl.Application.Inventory.Devices.Ports;
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
/// F7-T003: vínculo Task ↔ Device — 404 pt-BR (task/device/vínculo),
/// 409 em duplicidade, TaskDto com deviceIds e exclusão de device
/// bloqueada por tasks vinculadas.
/// </summary>
public sealed class TaskDeviceCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskDeviceCommandTests()
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
    private static DeviceRepository InventoryDeviceRepo(AppDbContext context) => new(context);

    private static async Task<(Guid TaskId, Guid DeviceId)> SeedTaskAndDeviceAsync(
        AppDbContext context, string ip = "192.0.2.71")
    {
        var location = Location.Create("Sala F7T003");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(
            location.Id, "sw-f7t003", DeviceType.Switch, ip: ip);
        context.Devices.Add(device);

        var task = TaskItem.Create("Tarefa com device", TaskKind.Maintenance);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return (task.Id, device.Id);
    }

    private static RecordingTaskHistoryWriter History() => new();

    private static LinkTaskDeviceCommandHandler LinkHandler(AppDbContext context)
        => new(TaskRepo(context), InventoryDeviceRepo(context), DeviceRepo(context),
            History(), NullLogger<LinkTaskDeviceCommandHandler>.Instance);

    private static UnlinkTaskDeviceCommandHandler UnlinkHandler(AppDbContext context)
        => new(TaskRepo(context), DeviceRepo(context), History(),
            NullLogger<UnlinkTaskDeviceCommandHandler>.Instance);

    private static GetTaskQueryHandler GetHandler(AppDbContext context)
        => new(TaskRepo(context), AssigneeRepo(context), DeviceRepo(context),
            IncidentRepo(context), new TaskKnowledgeArticleRepository(context), new TaskPdiRepository(context));

    // ── Link ─────────────────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireTasksManage()
    {
        new LinkTaskDeviceCommand(Guid.NewGuid(), Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new UnlinkTaskDeviceCommand(Guid.NewGuid(), Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
    }

    [Fact]
    public async Task Link_ShouldExposeDeviceInTaskDto()
    {
        await using var context = await CreateContextAsync();
        var (taskId, deviceId) = await SeedTaskAndDeviceAsync(context);

        await LinkHandler(context).Handle(
            new LinkTaskDeviceCommand(taskId, deviceId), CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetTaskQuery(taskId), CancellationToken.None);
        dto.DeviceCount.Should().Be(1);
        dto.DeviceIds.Should().Equal(deviceId);
    }

    [Fact]
    public async Task Link_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (_, deviceId) = await SeedTaskAndDeviceAsync(context);
        var unknown = Guid.NewGuid();

        var action = () => LinkHandler(context).Handle(
            new LinkTaskDeviceCommand(unknown, deviceId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Link_UnknownDevice_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedTaskAndDeviceAsync(context);
        var unknown = Guid.NewGuid();

        var action = () => LinkHandler(context).Handle(
            new LinkTaskDeviceCommand(taskId, unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Link_Duplicate_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, deviceId) = await SeedTaskAndDeviceAsync(context);

        await LinkHandler(context).Handle(
            new LinkTaskDeviceCommand(taskId, deviceId), CancellationToken.None);

        var action = () => LinkHandler(context).Handle(
            new LinkTaskDeviceCommand(taskId, deviceId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("O device já está vinculado à task.");
    }

    // ── Unlink ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Unlink_ShouldRemoveLinkAndClearDto()
    {
        await using var context = await CreateContextAsync();
        var (taskId, deviceId) = await SeedTaskAndDeviceAsync(context);
        await LinkHandler(context).Handle(
            new LinkTaskDeviceCommand(taskId, deviceId), CancellationToken.None);

        await UnlinkHandler(context).Handle(
            new UnlinkTaskDeviceCommand(taskId, deviceId), CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetTaskQuery(taskId), CancellationToken.None);
        dto.DeviceCount.Should().Be(0);
        dto.DeviceIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Unlink_UnknownLink_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedTaskAndDeviceAsync(context);
        var stranger = Guid.NewGuid();

        var action = () => UnlinkHandler(context).Handle(
            new UnlinkTaskDeviceCommand(taskId, stranger), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Vínculo da task '{taskId}' com o device "
                + $"'{stranger}' não encontrado.");
    }

    // ── Integrações com F2-T004 / F7-T001 ────────────────────────────────

    [Fact]
    public async Task DeleteDevice_WithLinkedTask_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, deviceId) = await SeedTaskAndDeviceAsync(context);
        await LinkHandler(context).Handle(
            new LinkTaskDeviceCommand(taskId, deviceId), CancellationToken.None);

        var action = () => new DeleteDeviceCommandHandler(
                InventoryDeviceRepo(context),
                NullLogger<DeleteDeviceCommandHandler>.Instance)
            .Handle(new DeleteDeviceCommand(deviceId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir o device: existem tasks vinculadas.");
    }

    [Fact]
    public async Task Search_ShouldEnrichPageWithDeviceIds()
    {
        await using var context = await CreateContextAsync();
        var (taskId, deviceId) = await SeedTaskAndDeviceAsync(context);
        await LinkHandler(context).Handle(
            new LinkTaskDeviceCommand(taskId, deviceId), CancellationToken.None);

        var handler = new SearchTasksQueryHandler(
            TaskRepo(context), AssigneeRepo(context), DeviceRepo(context),
            IncidentRepo(context), new TaskKnowledgeArticleRepository(context), new TaskPdiRepository(context));
        var page = await handler.Handle(
            new SearchTasksQuery(), CancellationToken.None);

        var item = page.Items.Should().ContainSingle().Subject;
        item.DeviceCount.Should().Be(1);
        item.DeviceIds.Should().Equal(deviceId);
    }
}

using FluentAssertions;
using InfraControl.Application.Incidents.Ports;
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
/// F7-T004: vínculo Task ↔ Incident — 404 pt-BR (task/incidente/vínculo),
/// 409 em duplicidade e TaskDto com incidentIds.
/// </summary>
public sealed class TaskIncidentCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskIncidentCommandTests()
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

    private static async Task<(Guid TaskId, Guid IncidentId)> SeedTaskAndIncidentAsync(
        AppDbContext context)
    {
        var task = TaskItem.Create("Tarefa pós-incidente", TaskKind.Incident);
        context.Tasks.Add(task);

        var incident = Incident.Create(
            "Link principal caiu", IncidentSeverity.High);
        context.Incidents.Add(incident);
        await context.SaveChangesAsync();
        return (task.Id, incident.Id);
    }

    private static RecordingTaskHistoryWriter History() => new();

    private static LinkTaskIncidentCommandHandler LinkHandler(AppDbContext context)
        => new(TaskRepo(context), IncidentRepo(context), IncidentLinkRepo(context),
            History(), NullLogger<LinkTaskIncidentCommandHandler>.Instance);

    private static UnlinkTaskIncidentCommandHandler UnlinkHandler(AppDbContext context)
        => new(TaskRepo(context), IncidentLinkRepo(context), History(),
            NullLogger<UnlinkTaskIncidentCommandHandler>.Instance);

    private static GetTaskQueryHandler GetHandler(AppDbContext context)
        => new(TaskRepo(context), AssigneeRepo(context), DeviceRepo(context),
            IncidentLinkRepo(context));

    // ── Link ─────────────────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireTasksManage()
    {
        new LinkTaskIncidentCommand(Guid.NewGuid(), Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new UnlinkTaskIncidentCommand(Guid.NewGuid(), Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
    }

    [Fact]
    public async Task Link_ShouldExposeIncidentInTaskDto()
    {
        await using var context = await CreateContextAsync();
        var (taskId, incidentId) = await SeedTaskAndIncidentAsync(context);

        await LinkHandler(context).Handle(
            new LinkTaskIncidentCommand(taskId, incidentId), CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetTaskQuery(taskId), CancellationToken.None);
        dto.IncidentCount.Should().Be(1);
        dto.IncidentIds.Should().Equal(incidentId);
    }

    [Fact]
    public async Task Link_UnknownTask_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (_, incidentId) = await SeedTaskAndIncidentAsync(context);
        var unknown = Guid.NewGuid();

        var action = () => LinkHandler(context).Handle(
            new LinkTaskIncidentCommand(unknown, incidentId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Link_UnknownIncident_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedTaskAndIncidentAsync(context);
        var unknown = Guid.NewGuid();

        var action = () => LinkHandler(context).Handle(
            new LinkTaskIncidentCommand(taskId, unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Incidente '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Link_Duplicate_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, incidentId) = await SeedTaskAndIncidentAsync(context);

        await LinkHandler(context).Handle(
            new LinkTaskIncidentCommand(taskId, incidentId), CancellationToken.None);

        var action = () => LinkHandler(context).Handle(
            new LinkTaskIncidentCommand(taskId, incidentId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("O incidente já está vinculado à task.");
    }

    // ── Unlink ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Unlink_ShouldRemoveLinkAndClearDto()
    {
        await using var context = await CreateContextAsync();
        var (taskId, incidentId) = await SeedTaskAndIncidentAsync(context);
        await LinkHandler(context).Handle(
            new LinkTaskIncidentCommand(taskId, incidentId), CancellationToken.None);

        await UnlinkHandler(context).Handle(
            new UnlinkTaskIncidentCommand(taskId, incidentId), CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetTaskQuery(taskId), CancellationToken.None);
        dto.IncidentCount.Should().Be(0);
        dto.IncidentIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Unlink_UnknownLink_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedTaskAndIncidentAsync(context);
        var stranger = Guid.NewGuid();

        var action = () => UnlinkHandler(context).Handle(
            new UnlinkTaskIncidentCommand(taskId, stranger), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Vínculo da task '{taskId}' com o incidente "
                + $"'{stranger}' não encontrado.");
    }

    // ── Integração com Search (F7-T001) ──────────────────────────────────

    [Fact]
    public async Task Search_ShouldEnrichPageWithIncidentIds()
    {
        await using var context = await CreateContextAsync();
        var (taskId, incidentId) = await SeedTaskAndIncidentAsync(context);
        await LinkHandler(context).Handle(
            new LinkTaskIncidentCommand(taskId, incidentId), CancellationToken.None);

        var handler = new SearchTasksQueryHandler(
            TaskRepo(context), AssigneeRepo(context), DeviceRepo(context),
            IncidentLinkRepo(context));
        var page = await handler.Handle(
            new SearchTasksQuery(), CancellationToken.None);

        var item = page.Items.Should().ContainSingle().Subject;
        item.IncidentCount.Should().Be(1);
        item.IncidentIds.Should().Equal(incidentId);
    }
}

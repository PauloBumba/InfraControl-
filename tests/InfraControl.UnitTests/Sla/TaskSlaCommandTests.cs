using FluentAssertions;
using InfraControl.Application.Sla.Policies.Commands;
using InfraControl.Application.Sla.TaskSlas;
using InfraControl.Application.Sla.TaskSlas.Commands;
using InfraControl.Application.Sla.TaskSlas.Queries;
using InfraControl.Application.Tasks.History;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using InfraControl.UnitTests.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Sla;

/// <summary>
/// F8-T003: ApplyTaskSlaCommand — 404 (task/política), 409 (já aplicado),
/// snapshot dos prazos, histórico `SlaApplied` e bloqueio de exclusão da
/// política com tasks aplicadas (409).
/// </summary>
public sealed class TaskSlaCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskSlaCommandTests()
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

    private static ApplyTaskSlaCommandHandler ApplyHandler(
        AppDbContext context, RecordingTaskHistoryWriter? history = null)
        => new(
            new TaskItemRepository(context),
            new SlaPolicyRepository(context),
            new TaskSlaRepository(context),
            history ?? new RecordingTaskHistoryWriter(),
            NullLogger<ApplyTaskSlaCommandHandler>.Instance);

    private static DeleteSlaPolicyCommandHandler DeletePolicyHandler(AppDbContext context)
        => new(
            new SlaPolicyRepository(context),
            new TaskSlaRepository(context),
            NullLogger<DeleteSlaPolicyCommandHandler>.Instance);

    private static async Task<(Guid TaskId, Guid PolicyId)> SeedAsync(AppDbContext context)
    {
        var task = TaskItem.Create("Task para SLA", TaskKind.Incident);
        context.Tasks.Add(task);

        var policy = SlaPolicy.Create("Padrão Incidentes", 30, 480);
        context.SlaPolicies.Add(policy);
        await context.SaveChangesAsync();
        return (task.Id, policy.Id);
    }

    [Fact]
    public async Task Apply_ShouldSnapshotDueDatesAndRecordHistory()
    {
        await using var context = await CreateContextAsync();
        var (taskId, policyId) = await SeedAsync(context);
        var history = new RecordingTaskHistoryWriter();

        var dto = await ApplyHandler(context, history).Handle(
            new ApplyTaskSlaCommand(taskId, policyId), CancellationToken.None);

        dto.TaskId.Should().Be(taskId);
        dto.SlaPolicyId.Should().Be(policyId);
        dto.ResponseMinutes.Should().Be(30);
        dto.ResolutionMinutes.Should().Be(480);

        var task = await context.Tasks.SingleAsync(t => t.Id == taskId);
        dto.ResponseDueAt.Should().Be(task.CreateDateTime.AddMinutes(30));

        var entry = history.Records.Should().ContainSingle().Subject;
        entry.Action.Should().Be(TaskHistoryAction.SlaApplied);
        entry.TaskId.Should().Be(taskId);
    }

    [Fact]
    public async Task Apply_Twice_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, policyId) = await SeedAsync(context);
        await ApplyHandler(context).Handle(
            new ApplyTaskSlaCommand(taskId, policyId), CancellationToken.None);

        var action = () => ApplyHandler(context).Handle(
            new ApplyTaskSlaCommand(taskId, policyId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um SLA aplicado a esta task.");
    }

    [Fact]
    public async Task Apply_UnknownTask_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (_, policyId) = await SeedAsync(context);

        var action = () => ApplyHandler(context).Handle(
            new ApplyTaskSlaCommand(Guid.NewGuid(), policyId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("Task '*' não encontrada.");
    }

    [Fact]
    public async Task Apply_UnknownPolicy_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedAsync(context);

        var action = () => ApplyHandler(context).Handle(
            new ApplyTaskSlaCommand(taskId, Guid.NewGuid()), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("Política de SLA '*' não encontrada.");
    }

    [Fact]
    public async Task Get_AppliedSla_ShouldReturnSnapshot()
    {
        await using var context = await CreateContextAsync();
        var (taskId, policyId) = await SeedAsync(context);
        await ApplyHandler(context).Handle(
            new ApplyTaskSlaCommand(taskId, policyId), CancellationToken.None);

        var dto = await new GetTaskSlaQueryHandler(new TaskSlaRepository(context))
            .Handle(new GetTaskSlaQuery(taskId), CancellationToken.None);

        dto.SlaPolicyId.Should().Be(policyId);
        dto.ResponseMinutes.Should().Be(30);
    }

    [Fact]
    public async Task Get_WithoutAppliedSla_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => new GetTaskSlaQueryHandler(new TaskSlaRepository(context))
            .Handle(new GetTaskSlaQuery(Guid.NewGuid()), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("SLA não aplicado à task '*'.");
    }

    [Fact]
    public async Task DeletePolicy_WithAppliedTaskSlas_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, policyId) = await SeedAsync(context);
        await ApplyHandler(context).Handle(
            new ApplyTaskSlaCommand(taskId, policyId), CancellationToken.None);

        var action = () => DeletePolicyHandler(context).Handle(
            new DeleteSlaPolicyCommand(policyId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage(
                "Não é possível excluir a política de SLA: existem tasks com este SLA aplicado.");
    }
}

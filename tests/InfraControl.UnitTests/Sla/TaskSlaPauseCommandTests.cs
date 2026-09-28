using FluentAssertions;
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
/// F8-T004: Pause/Resume — 404 sem SLA, 409 (pausa aberta duplicada /
/// resume sem pausa), extensão dos vencimentos e histórico.
/// </summary>
public sealed class TaskSlaPauseCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskSlaPauseCommandTests()
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

    private static PauseTaskSlaCommandHandler PauseHandler(
        AppDbContext context, RecordingTaskHistoryWriter? history = null)
        => new(
            new TaskSlaRepository(context),
            new TaskSlaPauseRepository(context),
            history ?? new RecordingTaskHistoryWriter(),
            NullLogger<PauseTaskSlaCommandHandler>.Instance);

    private static ResumeTaskSlaCommandHandler ResumeHandler(
        AppDbContext context, RecordingTaskHistoryWriter? history = null)
        => new(
            new TaskSlaRepository(context),
            new TaskSlaPauseRepository(context),
            history ?? new RecordingTaskHistoryWriter(),
            NullLogger<ResumeTaskSlaCommandHandler>.Instance);

    private static async Task<(Guid TaskId, Guid PolicyId)> SeedWithSlaAsync(AppDbContext context)
    {
        var task = TaskItem.Create("Task com SLA pausável", TaskKind.Incident);
        context.Tasks.Add(task);
        var policy = SlaPolicy.Create("Padrão", 30, 480);
        context.SlaPolicies.Add(policy);
        await context.SaveChangesAsync();

        var sla = TaskSla.Apply(task, policy);
        context.TaskSlas.Add(sla);
        await context.SaveChangesAsync();
        return (task.Id, policy.Id);
    }

    [Fact]
    public async Task PauseAndResume_ShouldClosePauseAndExtendDueDates()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedWithSlaAsync(context);
        var history = new RecordingTaskHistoryWriter();

        await PauseHandler(context, history)
            .Handle(new PauseTaskSlaCommand(taskId), CancellationToken.None);

        var open = await context.TaskSlasPauses.SingleAsync(p => p.TaskId == taskId);
        open.EndAt.Should().BeNull();

        // Data de pausa distinta do vencimento (padrão F7: SQL direto).
        var pausedAt = DateTime.UtcNow.AddMinutes(-45);
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE task_sla_pauses SET start_at = {0} WHERE id = {1}",
            pausedAt, open.Id);
        // O SQL não atualiza a instância já rastreada — recarrega para o
        // handler ver o start_at novo (padrão: mudanças via SQL exigem reload).
        await context.Entry(open).ReloadAsync();

        var slaBefore = await context.TaskSlas.SingleAsync(s => s.TaskId == taskId);
        var dueBefore = slaBefore.ResponseDueAt;

        await ResumeHandler(context, history)
            .Handle(new ResumeTaskSlaCommand(taskId), CancellationToken.None);

        var sla = await context.TaskSlas.SingleAsync(s => s.TaskId == taskId);
        sla.ResponseMinutes.Should().Be(30, "snapshot intacto");
        sla.ResponseDueAt.Should().BeCloseTo(
            dueBefore.Add(TimeSpan.FromMinutes(45)), TimeSpan.FromSeconds(5));

        history.Records.Select(r => r.Action)
            .Should().Equal(TaskHistoryAction.SlaPaused, TaskHistoryAction.SlaResumed);
        history.Records[1].Detail.Should().Contain("min nos prazos");
    }

    [Fact]
    public async Task Pause_WithoutAppliedSla_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => PauseHandler(context).Handle(
            new PauseTaskSlaCommand(Guid.NewGuid()), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("SLA não aplicado à task '*'.");
    }

    [Fact]
    public async Task Pause_TwiceWithoutResume_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedWithSlaAsync(context);
        await PauseHandler(context).Handle(
            new PauseTaskSlaCommand(taskId), CancellationToken.None);

        var action = () => PauseHandler(context).Handle(
            new PauseTaskSlaCommand(taskId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe uma pausa em aberto para o SLA desta task.");
    }

    [Fact]
    public async Task Resume_WithoutOpenPause_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedWithSlaAsync(context);

        var action = () => ResumeHandler(context).Handle(
            new ResumeTaskSlaCommand(taskId), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não existe pausa em aberto para o SLA desta task.");
    }

    [Fact]
    public async Task GetPauses_ShouldListChronologically()
    {
        await using var context = await CreateContextAsync();
        var (taskId, _) = await SeedWithSlaAsync(context);

        await PauseHandler(context).Handle(
            new PauseTaskSlaCommand(taskId), CancellationToken.None);
        await ResumeHandler(context).Handle(
            new ResumeTaskSlaCommand(taskId), CancellationToken.None);
        await PauseHandler(context).Handle(
            new PauseTaskSlaCommand(taskId), CancellationToken.None);

        var pauses = await new GetTaskSlaPausesQueryHandler(
                new TaskSlaRepository(context), new TaskSlaPauseRepository(context))
            .Handle(new GetTaskSlaPausesQuery(taskId), CancellationToken.None);

        pauses.Should().HaveCount(2);
        pauses.First().EndAt.Should().NotBeNull("a primeira pausa foi retomada");
        pauses.Last().EndAt.Should().BeNull("a segunda pausa segue aberta");
    }

    [Fact]
    public async Task GetPauses_WithoutAppliedSla_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => new GetTaskSlaPausesQueryHandler(
                new TaskSlaRepository(context), new TaskSlaPauseRepository(context))
            .Handle(new GetTaskSlaPausesQuery(Guid.NewGuid()), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("SLA não aplicado à task '*'.");
    }
}

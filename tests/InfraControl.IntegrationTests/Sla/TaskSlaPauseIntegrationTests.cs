using FluentAssertions;
using InfraControl.Application.Sla.Policies.Commands;
using InfraControl.Application.Sla.TaskSlas.Commands;
using InfraControl.Application.Sla.TaskSlas.Queries;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.History;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Sla;

/// <summary>
/// F8-T004: pausas do relógio de SLA ponta-a-ponta — pause/resume,
/// extensão dos vencimentos, histórico e 404/409/403.
/// </summary>
public sealed class TaskSlaPauseIntegrationTests : BaseIntegrationTest
{
    private static async Task<Guid> SeedTaskWithSlaAsync(IMediator mediator)
    {
        var task = await mediator.Send(
            new CreateTaskCommand("Task pausável", "Manual"));
        var policy = await mediator.Send(
            new CreateSlaPolicyCommand("Padrão Manual", 60, 480));
        await mediator.Send(new ApplyTaskSlaCommand(task.Id, policy.Id));
        return task.Id;
    }

    [Fact]
    public async Task PauseAndResume_ShouldExtendDueDatesAndRecordHistory()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var taskId = await SeedTaskWithSlaAsync(mediator);

        var before = await mediator.Send(new GetTaskSlaQuery(taskId));

        await mediator.Send(new PauseTaskSlaCommand(taskId));
        var openPauses = await mediator.Send(new GetTaskSlaPausesQuery(taskId));
        openPauses.Should().ContainSingle(p => p.EndAt == null);

        await mediator.Send(new ResumeTaskSlaCommand(taskId));
        var after = await mediator.Send(new GetTaskSlaQuery(taskId));

        after.ResponseMinutes.Should().Be(before.ResponseMinutes, "snapshot intacto");
        after.ResponseDueAt.Should().BeOnOrAfter(before.ResponseDueAt);
        after.ResolutionDueAt.Should().BeOnOrAfter(before.ResolutionDueAt);

        var entries = await mediator.Send(new GetTaskHistoryQuery(taskId));
        entries.Select(entry => entry.Action).Should().Contain(
            new[] { nameof(TaskHistoryAction.SlaPaused), nameof(TaskHistoryAction.SlaResumed) });
    }

    [Fact]
    public async Task Pause_TwiceWithoutResume_ShouldThrowConflictPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var taskId = await SeedTaskWithSlaAsync(mediator);
        await mediator.Send(new PauseTaskSlaCommand(taskId));

        var action = () => mediator.Send(new PauseTaskSlaCommand(taskId));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe uma pausa em aberto para o SLA desta task.");
    }

    [Fact]
    public async Task Pause_WithoutAppliedSla_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(new PauseTaskSlaCommand(unknown));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"SLA não aplicado à task '{unknown}'.");
    }

    [Fact]
    public async Task Resume_WithoutOpenPause_ShouldThrowConflictPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var taskId = await SeedTaskWithSlaAsync(mediator);

        var action = () => mediator.Send(new ResumeTaskSlaCommand(taskId));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não existe pausa em aberto para o SLA desta task.");
    }

    [Fact]
    public async Task Pause_WithoutSlaManage_ShouldFailPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoPermissions();

        var action = () => mediator.Send(new PauseTaskSlaCommand(Guid.NewGuid()));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Permissões ausentes: SlaManage");
    }
}

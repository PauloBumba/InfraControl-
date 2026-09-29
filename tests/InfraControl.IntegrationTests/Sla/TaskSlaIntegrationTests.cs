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
/// F8-T003: TaskSla ponta-a-ponta — aplicar snapshot de prazos, histórico
/// `SlaApplied`, 409 (duplicado/exclusão de política) e 403.
/// </summary>
public sealed class TaskSlaIntegrationTests : BaseIntegrationTest
{
    private static async Task<(Guid TaskId, Guid PolicyId)> SeedAsync(IMediator mediator)
    {
        var task = await mediator.Send(
            new CreateTaskCommand("Task com SLA", "Incident"));
        var policy = await mediator.Send(
            new CreateSlaPolicyCommand("Padrão Incidentes", 30, 480));
        return (task.Id, policy.Id);
    }

    [Fact]
    public async Task ApplySla_ShouldSnapshotAndRecordHistory()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var (taskId, policyId) = await SeedAsync(mediator);

        var applied = await mediator.Send(new ApplyTaskSlaCommand(taskId, policyId));
        applied.TaskId.Should().Be(taskId);
        applied.ResponseMinutes.Should().Be(30);
        applied.ResolutionMinutes.Should().Be(480);

        var fetched = await mediator.Send(new GetTaskSlaQuery(taskId));
        fetched.SlaPolicyId.Should().Be(policyId);
        // Postgres trunca microssegundos — comparação com tolerância.
        fetched.ResponseDueAt.Should().BeCloseTo(applied.ResponseDueAt, TimeSpan.FromSeconds(1));

        var entries = await mediator.Send(new GetTaskHistoryQuery(taskId));
        entries.Should().Contain(entry =>
            entry.Action == nameof(TaskHistoryAction.SlaApplied));
    }

    [Fact]
    public async Task ApplySla_Twice_ShouldThrowConflictPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var (taskId, policyId) = await SeedAsync(mediator);
        await mediator.Send(new ApplyTaskSlaCommand(taskId, policyId));

        var action = () => mediator.Send(new ApplyTaskSlaCommand(taskId, policyId));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um SLA aplicado a esta task.");
    }

    [Fact]
    public async Task DeletePolicy_WithAppliedSlas_ShouldThrowConflictPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var (taskId, policyId) = await SeedAsync(mediator);
        await mediator.Send(new ApplyTaskSlaCommand(taskId, policyId));

        var action = () => mediator.Send(new DeleteSlaPolicyCommand(policyId));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage(
                "Não é possível excluir a política de SLA: existem tasks com este SLA aplicado.");
    }

    [Fact]
    public async Task ApplySla_WithoutSlaManage_ShouldFailPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoRoles();

        var action = () => mediator.Send(
            new ApplyTaskSlaCommand(Guid.NewGuid(), Guid.NewGuid()));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator");
    }

    [Fact]
    public async Task GetSla_UnknownTask_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(new GetTaskSlaQuery(unknown));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"SLA não aplicado à task '{unknown}'.");
    }
}

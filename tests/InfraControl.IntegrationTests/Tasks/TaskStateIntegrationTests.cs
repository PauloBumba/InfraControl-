using FluentAssertions;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.History;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Tasks;

/// <summary>
/// F7-T007: workflow ponta-a-ponta — ciclo completo Open→…→Closed,
/// 409 fora do mapa, 400 estado desconhecido, 403 sem TasksManage e
/// histórico `StateChanged` com o detalhe "de → para".
/// </summary>
public sealed class TaskStateIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task StateLifecycle_OpenToClosed_ShouldSucceedAndRecordHistory()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var task = await mediator.Send(
            new CreateTaskCommand("Workflow de ponta a ponta", "Manual"));

        var inProgress = await mediator.Send(
            new ChangeTaskStateCommand(task.Id, "InProgress"));
        inProgress.State.Should().Be(nameof(TaskState.InProgress));

        var resolved = await mediator.Send(
            new ChangeTaskStateCommand(task.Id, "Resolved"));
        resolved.State.Should().Be(nameof(TaskState.Resolved));

        var closed = await mediator.Send(
            new ChangeTaskStateCommand(task.Id, "Closed"));
        closed.State.Should().Be(nameof(TaskState.Closed));

        var entries = await mediator.Send(new GetTaskHistoryQuery(task.Id));
        entries.Select(entry => entry.Action)
            .Should().Equal(
                nameof(TaskHistoryAction.Created),
                nameof(TaskHistoryAction.StateChanged),
                nameof(TaskHistoryAction.StateChanged),
                nameof(TaskHistoryAction.StateChanged));
        entries.Skip(1).Select(entry => entry.Detail)
            .Should().Equal(
                "Open → InProgress",
                "InProgress → Resolved",
                "Resolved → Closed");
    }

    [Fact]
    public async Task SameState_ShouldBeNoOpWithoutHistoryEntry()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var task = await mediator.Send(
            new CreateTaskCommand("No-op de estado", "Manual"));

        var dto = await mediator.Send(
            new ChangeTaskStateCommand(task.Id, "Open"));
        dto.State.Should().Be(nameof(TaskState.Open));

        var entries = await mediator.Send(new GetTaskHistoryQuery(task.Id));
        entries.Should().ContainSingle(); // só Created
    }

    [Fact]
    public async Task TransitionOutOfMap_ShouldThrowInvalidOperationPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var task = await mediator.Send(
            new CreateTaskCommand("Transição proibida", "Manual"));

        var action = () => mediator.Send(
            new ChangeTaskStateCommand(task.Id, "Closed"));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Transição inválida: 'Open' → 'Closed'.");
    }

    [Fact]
    public async Task UnknownStateValue_ShouldThrowArgumentExceptionPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var task = await mediator.Send(
            new CreateTaskCommand("Estado desconhecido", "Manual"));

        var action = () => mediator.Send(
            new ChangeTaskStateCommand(task.Id, "QualquerCoisa"));

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Estado inválido*");
    }

    [Fact]
    public async Task ChangeState_WithoutTasksManage_ShouldFailPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoRoles();

        var action = () => mediator.Send(
            new ChangeTaskStateCommand(Guid.NewGuid(), "InProgress"));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator");
    }

    [Fact]
    public async Task ChangeState_UnknownTask_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(
            new ChangeTaskStateCommand(unknown, "InProgress"));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{unknown}' não encontrada.");
    }
}

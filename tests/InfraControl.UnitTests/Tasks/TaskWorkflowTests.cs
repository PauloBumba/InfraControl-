using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Tasks;

/// <summary>
/// F7-T007: mapa de transições do workflow (docs/04) — só o mapa é
/// permitido; mesmo estado é no-op; fora do mapa → InvalidOperationException
/// (GlobalExceptionHandler → 409 pt-BR).
/// </summary>
public sealed class TaskWorkflowTests
{
    /// <summary>Seed por caminhos legais do próprio mapa (sem bypass).</summary>
    private static TaskItem TaskIn(TaskState state)
    {
        var task = TaskItem.Create("Task workflow", TaskKind.Manual);
        var path = state switch
        {
            TaskState.Open => Array.Empty<TaskState>(),
            TaskState.InProgress => [TaskState.InProgress],
            TaskState.Waiting => [TaskState.InProgress, TaskState.Waiting],
            TaskState.Blocked => [TaskState.InProgress, TaskState.Blocked],
            TaskState.Resolved => [TaskState.InProgress, TaskState.Resolved],
            TaskState.Cancelled => [TaskState.Cancelled],
            TaskState.Closed => [TaskState.InProgress, TaskState.Resolved, TaskState.Closed],
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };

        foreach (var step in path)
            task.ChangeState(step);

        return task;
    }

    [Theory]
    [InlineData(TaskState.InProgress)]
    [InlineData(TaskState.Cancelled)]
    public void Open_ShouldAllowDocumentedTransitions(TaskState target)
    {
        var task = TaskIn(TaskState.Open);

        var act = () => task.ChangeState(target);

        act.Should().NotThrow();
        task.State.Should().Be(target);
    }

    [Theory]
    [InlineData(TaskState.Open, TaskState.Resolved)]
    [InlineData(TaskState.Open, TaskState.Closed)]
    [InlineData(TaskState.InProgress, TaskState.Open)]
    [InlineData(TaskState.Waiting, TaskState.Resolved)]
    [InlineData(TaskState.Blocked, TaskState.Waiting)]
    [InlineData(TaskState.Resolved, TaskState.Cancelled)]
    [InlineData(TaskState.Cancelled, TaskState.Closed)]
    [InlineData(TaskState.Closed, TaskState.Cancelled)]
    public void TransitionOutOfMap_ShouldThrowInvalidOperationPtBr(
        TaskState from, TaskState to)
    {
        var task = TaskIn(from);

        var act = () => task.ChangeState(to);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Transição inválida: '{from}' → '{to}'.");
        task.State.Should().Be(from, "a transição inválida não pode mudar o estado");
    }

    [Fact]
    public void FullLifecycle_OpenToClosed_ShouldSucceed()
    {
        var task = TaskIn(TaskState.Open);

        task.ChangeState(TaskState.InProgress);
        task.ChangeState(TaskState.Resolved);
        task.ChangeState(TaskState.Closed);

        task.State.Should().Be(TaskState.Closed);
    }

    [Fact]
    public void ReopenClosed_ShouldReturnToOpen()
    {
        var task = TaskIn(TaskState.Closed);

        task.ChangeState(TaskState.Open);

        task.State.Should().Be(TaskState.Open);
    }

    [Fact]
    public void SameState_ShouldBeNoOpWithoutError()
    {
        var task = TaskIn(TaskState.InProgress);

        var act = () => task.ChangeState(TaskState.InProgress);

        act.Should().NotThrow();
        task.State.Should().Be(TaskState.InProgress);
    }

    [Fact]
    public void CanTransition_ShouldRejectSameStateAndUnknownPairs()
    {
        TaskStateWorkflow.CanTransition(TaskState.Open, TaskState.Open)
            .Should().BeFalse("mesmo estado é no-op, não transição");
        TaskStateWorkflow.CanTransition(TaskState.Open, TaskState.Resolved)
            .Should().BeFalse();
        TaskStateWorkflow.CanTransition(TaskState.InProgress, TaskState.Resolved)
            .Should().BeTrue();
    }
}

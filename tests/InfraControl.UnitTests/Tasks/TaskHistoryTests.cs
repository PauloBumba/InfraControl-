using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Tasks;

/// <summary>F7-T006: entrada do histórico — guards pt-BR.</summary>
public sealed class TaskHistoryTests
{
    [Fact]
    public void Create_ShouldMapTaskActionActorAndDetail()
    {
        var taskId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var entry = TaskHistory.Create(
            taskId, TaskHistoryAction.Assigned, actorId, "  user 123  ");

        entry.TaskId.Should().Be(taskId);
        entry.Action.Should().Be(TaskHistoryAction.Assigned);
        entry.ActorId.Should().Be(actorId);
        entry.Detail.Should().Be("user 123");
    }

    [Fact]
    public void Create_EmptyTaskId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskHistory.Create(
            Guid.Empty, TaskHistoryAction.Created);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id da task é obrigatório.*");
    }

    [Fact]
    public void Create_EmptyActorId_ShouldBecomeNull()
    {
        var entry = TaskHistory.Create(
            Guid.NewGuid(), TaskHistoryAction.Created, Guid.Empty);

        entry.ActorId.Should().BeNull();
    }

    [Fact]
    public void Create_BlankDetail_ShouldBecomeNull()
    {
        var entry = TaskHistory.Create(
            Guid.NewGuid(), TaskHistoryAction.Updated, detail: "   ");

        entry.Detail.Should().BeNull();
    }

    [Fact]
    public void Create_DetailAbove200_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskHistory.Create(
            Guid.NewGuid(), TaskHistoryAction.Updated, detail: new string('d', 201));

        action.Should().Throw<ArgumentException>()
            .WithMessage("*máximo 200 caracteres*");
    }
}

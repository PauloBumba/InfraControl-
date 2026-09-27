using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Tasks;

/// <summary>F7-T002: vínculo Task ↔ Usuário — guards pt-BR.</summary>
public sealed class TaskAssigneeTests
{
    [Fact]
    public void Create_ShouldPairTaskAndUser()
    {
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var assignee = TaskAssignee.Create(taskId, userId);

        assignee.TaskId.Should().Be(taskId);
        assignee.UserId.Should().Be(userId);
    }

    [Fact]
    public void Create_EmptyTaskId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskAssignee.Create(Guid.Empty, Guid.NewGuid());

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id da task é obrigatório.*");
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskAssignee.Create(Guid.NewGuid(), Guid.Empty);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id do usuário é obrigatório.*");
    }
}

using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Tasks;

/// <summary>F23-T005/P5b: vínculo Task ↔ PDI — guards pt-BR.</summary>
public sealed class TaskPdiTests
{
    [Fact]
    public void Create_ShouldPairTaskAndPdiOwner()
    {
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var link = TaskPdi.Create(taskId, userId);

        link.TaskId.Should().Be(taskId);
        link.UserId.Should().Be(userId);
    }

    [Fact]
    public void Create_EmptyTaskId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskPdi.Create(Guid.Empty, Guid.NewGuid());

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id da task é obrigatório.*");
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskPdi.Create(Guid.NewGuid(), Guid.Empty);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O usuário dono do PDI é obrigatório.*");
    }
}

using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Tasks;

/// <summary>
/// F7-T001: domínio da Task — guards pt-BR, nascimento em Open e
/// atualização de título/descrição.
/// </summary>
public sealed class TaskItemTests
{
    [Fact]
    public void Create_ShouldStartOpenWithGivenKind()
    {
        var task = TaskItem.Create(
            "Trocar switch do rack A", TaskKind.Maintenance, "agendada");

        task.Title.Should().Be("Trocar switch do rack A");
        task.Kind.Should().Be(TaskKind.Maintenance);
        task.State.Should().Be(TaskState.Open);
        task.Description.Should().Be("agendada");
    }

    [Fact]
    public void Create_WithoutTitle_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskItem.Create("   ", TaskKind.Manual);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O título da task é obrigatório.*");
    }

    [Fact]
    public void Create_TitleAbove120_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskItem.Create(new string('a', 121), TaskKind.Manual);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*máximo 120 caracteres*");
    }

    [Fact]
    public void Create_DescriptionAbove500_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskItem.Create(
            "t", TaskKind.Manual, new string('d', 501));

        action.Should().Throw<ArgumentException>()
            .WithMessage("*máximo 500 caracteres*");
    }

    [Fact]
    public void Update_ShouldTrimTitleAndClearBlankDescription()
    {
        var task = TaskItem.Create("original", TaskKind.Manual);

        task.Update("  novo título  ", "   ");

        task.Title.Should().Be("novo título");
        task.Description.Should().BeNull();
    }

    [Fact]
    public void ChangeState_ShouldMoveAndNoOpOnSameState()
    {
        var task = TaskItem.Create("t", TaskKind.Manual);

        task.ChangeState(TaskState.InProgress);
        task.State.Should().Be(TaskState.InProgress);

        task.ChangeState(TaskState.InProgress);
        task.State.Should().Be(TaskState.InProgress);
    }
}

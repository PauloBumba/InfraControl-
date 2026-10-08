using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.UnitTests.Tasks;

/// <summary>
/// F23-T003: critério de aceite da Task (§12) — guards pt-BR do domínio,
/// carimbo de atendimento e eventos CRUD (ADR-030 §5).
/// </summary>
public sealed class TaskCriterionTests
{
    [Fact]
    public void Create_ShouldPairTaskAndTrimDescription()
    {
        var taskId = Guid.NewGuid();

        var criterion = TaskCriterion.Create(taskId, "  contrato assinado  ");

        criterion.TaskId.Should().Be(taskId);
        criterion.Description.Should().Be("contrato assinado");
        criterion.IsDone.Should().BeFalse();
        criterion.DoneAt.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldRaiseCriterionCreatedEvent()
    {
        var criterion = TaskCriterion.Create(Guid.NewGuid(), "ok");

        criterion.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TaskCriterionCreatedDomainEvent>();
    }

    [Fact]
    public void Create_EmptyTaskId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskCriterion.Create(Guid.Empty, "ok");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id da task é obrigatório.*");
    }

    [Fact]
    public void Create_BlankDescription_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskCriterion.Create(Guid.NewGuid(), "   ");

        action.Should().Throw<ArgumentException>()
            .WithMessage("A descrição do critério é obrigatória.*");
    }

    [Fact]
    public void Create_DescriptionAbove300_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskCriterion.Create(
            Guid.NewGuid(), new string('c', 301));

        action.Should().Throw<ArgumentException>()
            .WithMessage("*máximo 300 caracteres*");
    }

    [Fact]
    public void EditDescription_ShouldTrimAndReplaceIt()
    {
        var criterion = TaskCriterion.Create(Guid.NewGuid(), "antigo");

        criterion.EditDescription("  critério novo  ");

        criterion.Description.Should().Be("critério novo");
    }

    [Fact]
    public void EditDescription_Blank_ShouldThrowArgumentPtBr()
    {
        var criterion = TaskCriterion.Create(Guid.NewGuid(), "antigo");

        var action = () => criterion.EditDescription("  ");

        action.Should().Throw<ArgumentException>()
            .WithMessage("A descrição do critério é obrigatória.*");
        criterion.Description.Should().Be("antigo");
    }

    [Fact]
    public void SetDone_ShouldStampDoneAtAndClearOnUndo()
    {
        var criterion = TaskCriterion.Create(Guid.NewGuid(), "teste passando");

        criterion.SetDone(true);
        criterion.IsDone.Should().BeTrue();
        criterion.DoneAt.Should().NotBeNull();

        criterion.SetDone(false);
        criterion.IsDone.Should().BeFalse();
        criterion.DoneAt.Should().BeNull();
    }

    [Fact]
    public void Delete_ShouldRaiseCriterionDeletedEvent()
    {
        var criterion = TaskCriterion.Create(Guid.NewGuid(), "ok");

        criterion.Delete();

        // O evento de Create ainda está na lista (sem ClearDomainEvents) —
        // o assert é que o Delete adiciona exatamente o de exclusão.
        criterion.DomainEvents.Should().ContainSingle(
            e => e is TaskCriterionDeletedDomainEvent);
    }
}

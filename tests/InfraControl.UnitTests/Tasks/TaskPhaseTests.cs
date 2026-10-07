using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.UnitTests.Tasks;

/// <summary>
/// F23-T004/P4d — fase/etapa dentro da task (§7): guards de nome/posição,
/// ponteiro de fase atual, carimbo de conclusão e eventos CRUD.
/// </summary>
public class TaskPhaseTests
{
    [Fact]
    public void Create_ShouldSetMetadataAndRaiseCreatedEvent()
    {
        var taskId = Guid.NewGuid();

        var phase = TaskPhase.Create(taskId, "Diagnóstico", 0);

        phase.TaskId.Should().Be(taskId);
        phase.Name.Should().Be("Diagnóstico");
        phase.Position.Should().Be(0);
        phase.IsCurrent.Should().BeFalse();
        phase.IsCompleted.Should().BeFalse();
        phase.CompletedAt.Should().BeNull();
        phase.DomainEvents.Should()
            .ContainSingle()
            .Which.Should().BeOfType<TaskPhaseCreatedDomainEvent>();
    }

    [Fact]
    public void Create_EmptyTaskId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskPhase.Create(Guid.Empty, "Fase", 0);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id da task é obrigatório.*");
    }

    [Fact]
    public void Create_NegativePosition_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskPhase.Create(Guid.NewGuid(), "Fase", -1);

        action.Should().Throw<ArgumentException>()
            .WithMessage("A posição da fase é inválida.*");
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskPhase.Create(Guid.NewGuid(), "   ", 0);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O nome da fase é obrigatório.*");
    }

    [Fact]
    public void Create_NameAboveMax_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskPhase.Create(
            Guid.NewGuid(), new string('f', 121), 0);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*no máximo 120 caracteres*");
    }

    [Fact]
    public void Rename_ShouldTrimAndRaiseUpdatedEvent()
    {
        var phase = TaskPhase.Create(Guid.NewGuid(), "Antes", 0);

        phase.Rename("  Depois  ");

        phase.Name.Should().Be("Depois");
        phase.DomainEvents.Should()
            .Contain(domainEvent => domainEvent is TaskPhaseUpdatedDomainEvent);
    }

    [Fact]
    public void SetCurrent_ShouldTogglePointer()
    {
        var phase = TaskPhase.Create(Guid.NewGuid(), "Fase", 0);

        phase.SetCurrent(true);
        phase.IsCurrent.Should().BeTrue();

        phase.SetCurrent(false);
        phase.IsCurrent.Should().BeFalse();
    }

    [Fact]
    public void Complete_ThenReopen_ShouldStampAndClear()
    {
        var phase = TaskPhase.Create(Guid.NewGuid(), "Fase", 0);

        phase.Complete();
        phase.IsCompleted.Should().BeTrue();
        phase.CompletedAt.Should().NotBeNull();

        phase.Reopen();
        phase.IsCompleted.Should().BeFalse();
        phase.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Delete_ShouldRaiseDeletedEvent()
    {
        var phase = TaskPhase.Create(Guid.NewGuid(), "Fase", 0);

        phase.Delete();

        // O evento de Create continua na lista — o Delete entra junto.
        phase.DomainEvents.Should()
            .Contain(domainEvent => domainEvent is TaskPhaseDeletedDomainEvent);
    }
}

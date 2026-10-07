using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.UnitTests.Tasks;

/// <summary>
/// F23-T004/P4b — impedimento estruturado (§10): guards de descrição
/// (300), carimbo de resolução e eventos CRUD (ADR-030).
/// </summary>
public class TaskImpedimentTests
{
    [Fact]
    public void Create_ShouldRaiseCreatedEvent()
    {
        var impediment = TaskImpediment.Create(
            Guid.NewGuid(), "Servidor sem energia no rack 3");

        impediment.Id.Should().NotBe(Guid.Empty);
        impediment.IsResolved.Should().BeFalse();
        impediment.ResolvedAt.Should().BeNull();
        impediment.DomainEvents.Should()
            .ContainSingle()
            .Which.Should().BeOfType<TaskImpedimentCreatedDomainEvent>();
    }

    [Fact]
    public void Create_EmptyTaskId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskImpediment.Create(Guid.Empty, "Qualquer");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id da task é obrigatório.*");
    }

    [Fact]
    public void Create_EmptyDescription_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskImpediment.Create(Guid.NewGuid(), "   ");

        action.Should().Throw<ArgumentException>()
            .WithMessage("A descrição do impedimento é obrigatória.*");
    }

    [Fact]
    public void Create_DescriptionAboveMax_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskImpediment.Create(
            Guid.NewGuid(), new string('x', 301));

        action.Should().Throw<ArgumentException>()
            .WithMessage("*no máximo 300 caracteres.*");
    }

    [Fact]
    public void Create_ShouldTrimDescription()
    {
        var impediment = TaskImpediment.Create(
            Guid.NewGuid(), "  Aguardando aprovação  ");

        impediment.Description.Should().Be("Aguardando aprovação");
    }

    [Fact]
    public void EditDescription_ShouldUpdateAndRaiseUpdatedEvent()
    {
        var impediment = TaskImpediment.Create(Guid.NewGuid(), "Original");

        impediment.EditDescription("  Revisado  ");

        impediment.Description.Should().Be("Revisado");
        impediment.DomainEvents.Should()
            .Contain(domainEvent => domainEvent is TaskImpedimentUpdatedDomainEvent);
    }

    [Fact]
    public void SetResolved_True_ShouldStampResolvedAt()
    {
        var impediment = TaskImpediment.Create(Guid.NewGuid(), "Bloqueio");

        impediment.SetResolved(true);

        impediment.IsResolved.Should().BeTrue();
        impediment.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public void SetResolved_False_ShouldClearStamp()
    {
        var impediment = TaskImpediment.Create(Guid.NewGuid(), "Bloqueio");
        impediment.SetResolved(true);

        impediment.SetResolved(false);

        impediment.IsResolved.Should().BeFalse();
        impediment.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void Delete_ShouldRaiseDeletedEvent()
    {
        var impediment = TaskImpediment.Create(Guid.NewGuid(), "Bloqueio");

        impediment.Delete();

        // O evento de Create continua na lista — o Delete entra junto.
        impediment.DomainEvents.Should()
            .Contain(domainEvent => domainEvent is TaskImpedimentDeletedDomainEvent);
    }
}

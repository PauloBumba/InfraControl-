using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.UnitTests.Checklists;

/// <summary>
/// F50-T007/ADR-067: item de checklist genérico — guards pt-BR do
/// domínio espelhando o TaskCriterion (F23-T003), carimbo de
/// atendimento e eventos CRUD (ADR-030 §5), com dono polimórfico.
/// </summary>
public sealed class ChecklistItemTests
{
    [Fact]
    public void Create_ShouldPairOwnerAndTrimDescription()
    {
        var ownerId = Guid.NewGuid();

        var item = ChecklistItem.Create(
            ChecklistOwnerType.Device, ownerId, "  firmware atualizado  ");

        item.OwnerType.Should().Be(ChecklistOwnerType.Device);
        item.OwnerId.Should().Be(ownerId);
        item.Description.Should().Be("firmware atualizado");
        item.IsDone.Should().BeFalse();
        item.DoneAt.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldRaiseItemCreatedEvent()
    {
        var item = ChecklistItem.Create(
            ChecklistOwnerType.Location, Guid.NewGuid(), "ok");

        item.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ChecklistItemCreatedDomainEvent>();
    }

    [Fact]
    public void Create_EmptyOwnerId_ShouldThrowArgumentPtBr()
    {
        var action = () => ChecklistItem.Create(
            ChecklistOwnerType.Incident, Guid.Empty, "ok");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id do dono é obrigatório.*");
    }

    [Fact]
    public void Create_BlankDescription_ShouldThrowArgumentPtBr()
    {
        var action = () => ChecklistItem.Create(
            ChecklistOwnerType.Project, Guid.NewGuid(), "   ");

        action.Should().Throw<ArgumentException>()
            .WithMessage("A descrição do item é obrigatória.*");
    }

    [Fact]
    public void Create_DescriptionAbove300_ShouldThrowArgumentPtBr()
    {
        var action = () => ChecklistItem.Create(
            ChecklistOwnerType.Device, Guid.NewGuid(), new string('c', 301));

        action.Should().Throw<ArgumentException>()
            .WithMessage("*máximo 300 caracteres*");
    }

    [Fact]
    public void EditDescription_ShouldTrimAndReplaceIt()
    {
        var item = ChecklistItem.Create(
            ChecklistOwnerType.Device, Guid.NewGuid(), "antigo");

        item.EditDescription("  item novo  ");

        item.Description.Should().Be("item novo");
    }

    [Fact]
    public void EditDescription_Blank_ShouldThrowArgumentPtBr()
    {
        var item = ChecklistItem.Create(
            ChecklistOwnerType.Device, Guid.NewGuid(), "antigo");

        var action = () => item.EditDescription("  ");

        action.Should().Throw<ArgumentException>()
            .WithMessage("A descrição do item é obrigatória.*");
        item.Description.Should().Be("antigo");
    }

    [Fact]
    public void SetDone_ShouldStampDoneAtAndClearOnUndo()
    {
        var item = ChecklistItem.Create(
            ChecklistOwnerType.Device, Guid.NewGuid(), "teste passando");

        item.SetDone(true);
        item.IsDone.Should().BeTrue();
        item.DoneAt.Should().NotBeNull();

        item.SetDone(false);
        item.IsDone.Should().BeFalse();
        item.DoneAt.Should().BeNull();
    }

    [Fact]
    public void Delete_ShouldRaiseItemDeletedEvent()
    {
        var item = ChecklistItem.Create(
            ChecklistOwnerType.Device, Guid.NewGuid(), "ok");

        item.Delete();

        // O evento de Create ainda está na lista (sem ClearDomainEvents) —
        // o assert é que o Delete adiciona exatamente o de exclusão.
        item.DomainEvents.Should().ContainSingle(
            e => e is ChecklistItemDeletedDomainEvent);
    }
}

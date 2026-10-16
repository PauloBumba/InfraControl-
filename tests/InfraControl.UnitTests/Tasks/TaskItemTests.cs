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
    public void Create_WithCreator_ShouldPersistCreatedById()
    {
        var creatorId = Guid.NewGuid();

        var task = TaskItem.Create(
            "Instalar AP no 2º andar", TaskKind.Maintenance,
            createdById: creatorId);

        task.CreatedById.Should().Be(creatorId);
    }

    [Fact]
    public void Create_WithoutCreator_ShouldKeepCreatedByIdNull()
    {
        var task = TaskItem.Create("Sem criador explícito", TaskKind.Manual);

        task.CreatedById.Should().BeNull();
    }

    [Fact]
    public void Create_EmptyCreator_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskItem.Create(
            "t", TaskKind.Manual, createdById: Guid.Empty);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id do criador não pode ser vazio.*");
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

    // ── F23-T001: identificação básica ───────────────────────────────────

    [Fact]
    public void Create_ShouldDefaultToMediumWithoutDueDateOrResponsible()
    {
        var task = TaskItem.Create("identificação", TaskKind.Manual);

        task.Priority.Should().Be(TaskPriority.Medium);
        task.DueDate.Should().BeNull();
        task.ResponsibleId.Should().BeNull();
    }

    [Fact]
    public void Create_WithIdentification_ShouldPersistAllFields()
    {
        var responsible = Guid.NewGuid();
        var due = new DateOnly(2026, 10, 20);

        var task = TaskItem.Create(
            "com identificação", TaskKind.Project, priority: TaskPriority.Critical,
            dueDate: due, responsibleId: responsible);

        task.Priority.Should().Be(TaskPriority.Critical);
        task.DueDate.Should().Be(due);
        task.ResponsibleId.Should().Be(responsible);
    }

    [Fact]
    public void Setters_ShouldReplaceValuesIncludingClearingOptionals()
    {
        var task = TaskItem.Create("t", TaskKind.Manual);
        var responsible = Guid.NewGuid();

        task.SetPriority(TaskPriority.High);
        task.SetDueDate(new DateOnly(2026, 11, 2));
        task.SetResponsible(responsible);

        task.Priority.Should().Be(TaskPriority.High);
        task.DueDate.Should().Be(new DateOnly(2026, 11, 2));
        task.ResponsibleId.Should().Be(responsible);

        task.SetPriority(TaskPriority.Low);
        task.SetDueDate(null);
        task.SetResponsible(null);

        task.Priority.Should().Be(TaskPriority.Low);
        task.DueDate.Should().BeNull();
        task.ResponsibleId.Should().BeNull();
    }

    [Fact]
    public void SetResponsible_WithEmptyGuid_ShouldThrowArgumentPtBr()
    {
        var task = TaskItem.Create("t", TaskKind.Manual);

        var action = () => task.SetResponsible(Guid.Empty);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id do usuário é obrigatório.*");
    }

    [Fact]
    public void SetDueDate_WithPastDate_ShouldBeAllowed()
    {
        // Task atrasada ≠ erro de domínio (o prazo é registro, não guarda).
        var task = TaskItem.Create("t", TaskKind.Manual);

        task.SetDueDate(new DateOnly(2020, 1, 1));

        task.DueDate.Should().Be(new DateOnly(2020, 1, 1));
    }
}

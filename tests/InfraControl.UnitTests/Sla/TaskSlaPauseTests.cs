using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Sla;

/// <summary>
/// F8-T004: TaskSlaPause — pausa em aberto, fechamento com duração e
/// extensão dos vencimentos do TaskSla (snapshot de minutos intacto).
/// </summary>
public sealed class TaskSlaPauseTests
{
    [Fact]
    public void Start_ShouldCreateOpenPauseWithStartAt()
    {
        var pause = TaskSlaPause.Start(Guid.NewGuid());

        pause.IsOpen.Should().BeTrue();
        pause.EndAt.Should().BeNull();
        pause.Duration.Should().Be(TimeSpan.Zero, "pausa aberta não conta");
        pause.StartAt.Should().NotBe(default);
    }

    [Fact]
    public void Start_EmptyTaskId_ShouldThrowArgumentExceptionPtBr()
    {
        var act = () => TaskSlaPause.Start(Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id da task é obrigatório.*");
    }

    [Fact]
    public void Close_ShouldEndPauseAndComputeDuration()
    {
        var pause = TaskSlaPause.Start(Guid.NewGuid());

        pause.Close();

        pause.IsOpen.Should().BeFalse();
        pause.EndAt.Should().NotBeNull();
        pause.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void Close_Twice_ShouldThrowInvalidOperationPtBr()
    {
        var pause = TaskSlaPause.Start(Guid.NewGuid());
        pause.Close();

        var act = () => pause.Close();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("A pausa já está encerrada.");
    }

    [Fact]
    public void ExtendDueDates_ShouldSumPauseToBothDueDates()
    {
        var task = TaskItem.Create("Task pausa", Domain.Enums.TaskKind.Manual);
        var policy = SlaPolicy.Create("P", 30, 480);
        var sla = TaskSla.Apply(task, policy);
        var originalResponse = sla.ResponseDueAt;
        var originalResolution = sla.ResolutionDueAt;

        sla.ExtendDueDates(TimeSpan.FromMinutes(45));
        sla.ExtendDueDates(TimeSpan.Zero); // ignorada

        sla.ResponseDueAt.Should().Be(originalResponse.AddMinutes(45));
        sla.ResolutionDueAt.Should().Be(originalResolution.AddMinutes(45));
        sla.ResponseMinutes.Should().Be(30, "snapshot de minutos não muda");
        sla.ResolutionMinutes.Should().Be(480);
    }
}

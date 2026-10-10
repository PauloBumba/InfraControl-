using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.UnitTests.Projects;

public sealed class ProjectPhaseTests
{
    private readonly Guid _projectId = Guid.NewGuid();

    [Fact]
    public void Create_ComDadosValidos_CriaFaseNotStartedComEvento()
    {
        var phase = ProjectPhase.Create(
            _projectId,
            "Levantamento",
            order: 1,
            description: "Inventário e desenho",
            completionCriteria: "Topologia aprovada",
            plannedStartDate: new DateOnly(2026, 11, 1),
            plannedEndDate: new DateOnly(2026, 11, 15));

        phase.Id.Should().NotBeEmpty();
        phase.ProjectId.Should().Be(_projectId);
        phase.Title.Should().Be("Levantamento");
        phase.Order.Should().Be(1);
        phase.Status.Should().Be(ProjectPhaseStatus.NotStarted);
        phase.Percent.Should().Be(0);
        phase.CompletionCriteria.Should().Be("Topologia aprovada");
        phase.DomainEvents.Should().ContainSingle(e => e is ProjectPhaseCreatedDomainEvent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ComTituloVazio_LancaArgumentException(string title)
    {
        var act = () => ProjectPhase.Create(_projectId, title, 1);
        act.Should().Throw<ArgumentException>().WithMessage("*título*");
    }

    [Fact]
    public void Create_ComProjectIdVazio_LancaArgumentException()
    {
        var act = () => ProjectPhase.Create(Guid.Empty, "Fase", 1);
        act.Should().Throw<ArgumentException>().WithMessage("*projeto*");
    }

    [Fact]
    public void Start_Submit_Validate_PercorreFluxoEEmiteEventos()
    {
        var phase = ProjectPhase.Create(_projectId, "Implantação", 1);
        phase.ClearDomainEvents();

        phase.Start();
        phase.Status.Should().Be(ProjectPhaseStatus.InProgress);
        phase.ActualStartDate.Should().NotBeNull();

        phase.SetProgress(40);
        phase.Percent.Should().Be(40);

        phase.Submit();
        phase.Status.Should().Be(ProjectPhaseStatus.PendingValidation);
        phase.Percent.Should().Be(100);

        phase.Validate();
        phase.Status.Should().Be(ProjectPhaseStatus.Completed);
        phase.ActualEndDate.Should().NotBeNull();

        phase.DomainEvents.OfType<ProjectPhaseStatusChangedDomainEvent>()
            .Should().HaveCount(3);
    }

    [Fact]
    public void Reject_DePendingValidation_VoltaParaInProgress()
    {
        var phase = ProjectPhase.Create(_projectId, "Testes", 1);
        phase.Start();
        phase.Submit();

        phase.Reject("Faltam evidências de failover");

        phase.Status.Should().Be(ProjectPhaseStatus.InProgress);
    }

    [Fact]
    public void Reject_SemMotivo_LancaArgumentException()
    {
        var phase = ProjectPhase.Create(_projectId, "Testes", 1);
        phase.Start();
        phase.Submit();

        var act = () => phase.Reject("  ");
        act.Should().Throw<ArgumentException>().WithMessage("*motivo*");
    }

    [Fact]
    public void Update_QuandoCompleted_LancaInvalidOperationException()
    {
        var phase = ProjectPhase.Create(_projectId, "Fase", 1);
        phase.Start();
        phase.Submit();
        phase.Validate();

        var act = () => phase.Update("Novo", null, null, null, null, null);
        act.Should().Throw<InvalidOperationException>().WithMessage("*concluída*");
    }

    [Fact]
    public void SetProgress_EmNotStarted_IniciaAFase()
    {
        var phase = ProjectPhase.Create(_projectId, "Fase", 1);

        phase.SetProgress(10);

        phase.Status.Should().Be(ProjectPhaseStatus.InProgress);
        phase.Percent.Should().Be(10);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void SetProgress_ForaDoIntervalo_LancaArgumentOutOfRange(int percent)
    {
        var phase = ProjectPhase.Create(_projectId, "Fase", 1);
        var act = () => phase.SetProgress(percent);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

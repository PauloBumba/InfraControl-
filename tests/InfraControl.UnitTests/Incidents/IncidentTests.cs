using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Incidents;

/// <summary>
/// F6-T003: Incident — agrupa N Alerts; estados da UI (5) com transições
/// livres; auto-abertura vem do avaliador (Critical).
/// </summary>
public sealed class IncidentTests
{
    [Fact]
    public void Create_ShouldOpenIncidentWithSeverityAndDevice()
    {
        var device = Guid.NewGuid();
        var incident = Incident.Create(
            "Device 'sw-01' fora do ar (3 falhas consecutivas no ping).",
            IncidentSeverity.Critical,
            deviceId: device);

        incident.Title.Should().Contain("sw-01");
        incident.Severity.Should().Be(IncidentSeverity.Critical);
        incident.Status.Should().Be(IncidentStatus.Open);
        incident.DeviceId.Should().Be(device);
        incident.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithoutTitle_ShouldThrowArgumentErrorPtBr()
    {
        var action = () => Incident.Create("  ", IncidentSeverity.High);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O título do incidente é obrigatório.*");
    }

    [Fact]
    public void ChangeStatus_ShouldMoveThroughUiStates()
    {
        var incident = Incident.Create("t", IncidentSeverity.High);

        incident.ChangeStatus(IncidentStatus.Identified);

        incident.Status.Should().Be(IncidentStatus.Identified);
        incident.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void ChangeStatus_ToSameStatus_ShouldThrowArgumentErrorPtBr()
    {
        var incident = Incident.Create("t", IncidentSeverity.High);

        var action = () => incident.ChangeStatus(IncidentStatus.Open);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*já está no status 'Open'*");
    }

    [Fact]
    public void Resolve_ShouldStampResolvedAt_AndReopenClearsIt()
    {
        var incident = Incident.Create("t", IncidentSeverity.Critical);

        incident.ChangeStatus(IncidentStatus.Resolved);
        incident.ResolvedAt.Should().NotBeNull();

        incident.ChangeStatus(IncidentStatus.Investigating);
        incident.ResolvedAt.Should().BeNull("reopen limpa o timestamp");
    }

    [Fact]
    public void Acknowledge_ShouldMoveToInvestigating()
    {
        var incident = Incident.Create("t", IncidentSeverity.Critical);

        incident.Acknowledge();

        incident.Status.Should().Be(IncidentStatus.Investigating);
    }

    [Fact]
    public void IncidentAlert_ShouldGuardEmptyIdsPtBr()
    {
        var action = () => IncidentAlert.Create(Guid.Empty, Guid.NewGuid());

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id do incidente é obrigatório.*");
    }
}

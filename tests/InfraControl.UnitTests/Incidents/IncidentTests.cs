using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

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
    public void Create_ShouldRaiseIncidentCreatedDomainEvent()
    {
        var device = Guid.NewGuid();
        var incident = Incident.Create(
            "Carga de CPU alta", IncidentSeverity.Critical, device,
            "Recebido do Zabbix.");

        incident.DomainEvents.Should().HaveCount(1);
        var domainEvent = incident.DomainEvents.OfType<IncidentCreatedDomainEvent>().Single();
        domainEvent.IncidentId.Should().Be(incident.Id);
        domainEvent.Title.Should().Be("Carga de CPU alta");
        domainEvent.Severity.Should().Be(IncidentSeverity.Critical);
        domainEvent.DeviceId.Should().Be(device);
        domainEvent.EventType.Should().Be(nameof(IncidentCreatedDomainEvent));
        domainEvent.EventId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_WithoutDevice_ShouldRaiseEventWithNullDevice()
    {
        var incident = Incident.Create("Host desconhecido", IncidentSeverity.Low);

        var domainEvent = incident.DomainEvents.OfType<IncidentCreatedDomainEvent>().Single();
        domainEvent.DeviceId.Should().BeNull();
    }

    [Fact]
    public void ChangeStatus_ShouldNotRaiseCreatedEventAgain()
    {
        var incident = Incident.Create("Para resolver", IncidentSeverity.Medium);
        incident.ClearDomainEvents();

        incident.ChangeStatus(IncidentStatus.Resolved);

        incident.DomainEvents.OfType<IncidentCreatedDomainEvent>().Should().BeEmpty();

        // F19-T007: a resolução levanta IncidentResolvedDomainEvent
        // (notificação Operator + Admin); outros status não levantam nada.
        var resolved = incident.DomainEvents.OfType<IncidentResolvedDomainEvent>().Single();
        resolved.IncidentId.Should().Be(incident.Id);
        resolved.Title.Should().Be("Para resolver");
    }

    [Fact]
    public void ChangeStatus_NonResolvedStatus_ShouldNotRaiseResolvedEvent()
    {
        var incident = Incident.Create("Investigar", IncidentSeverity.Low);
        incident.ClearDomainEvents();

        incident.ChangeStatus(IncidentStatus.Investigating);

        incident.DomainEvents.Should().BeEmpty();
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

    // ── F6-T005: causa provável (RootCause) ──────────────────────────────

    [Fact]
    public void SetRootCause_ShouldStoreTrimmedValue()
    {
        var incident = Incident.Create("t", IncidentSeverity.Critical);

        incident.SetRootCause("  Falha no upstream 'core'.  ");

        incident.RootCause.Should().Be("Falha no upstream 'core'.");
    }

    [Fact]
    public void SetRootCause_WithNull_ShouldClear()
    {
        var incident = Incident.Create("t", IncidentSeverity.Critical);
        incident.SetRootCause("antiga");

        incident.SetRootCause(null);

        incident.RootCause.Should().BeNull();
    }

    [Fact]
    public void SetRootCause_AboveMaxLength_ShouldThrowArgumentErrorPtBr()
    {
        var incident = Incident.Create("t", IncidentSeverity.Critical);

        var action = () => incident.SetRootCause(new string('x', 501));

        action.Should().Throw<ArgumentException>()
            .WithMessage("A causa provável deve ter no máximo 500 caracteres.*");
    }

    [Fact]
    public void SetRootCause_SameValue_ShouldKeepOriginal()
    {
        var incident = Incident.Create("t", IncidentSeverity.Critical);
        incident.SetRootCause("Falha local no device.");

        incident.SetRootCause("Falha local no device.");

        incident.RootCause.Should().Be("Falha local no device.");
    }
}

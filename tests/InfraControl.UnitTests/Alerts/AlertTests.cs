using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.UnitTests.Alerts;

/// <summary>
/// F6-T001: Alert — condição detectada (glossário) nasce Open e resolve
/// quando a condição normaliza (auto-resolve aprovado pelo usuário).
/// </summary>
public sealed class AlertTests
{
    [Fact]
    public void Create_ShouldOpenAlertWithSeverityAndKind()
    {
        var device = Guid.NewGuid();
        var alert = Alert.Create(
            device, CheckKind.Ping, AlertSeverity.Critical,
            "Device 'sw-01' está fora do ar (ping falhou).");

        alert.DeviceId.Should().Be(device);
        alert.Kind.Should().Be(CheckKind.Ping);
        alert.Severity.Should().Be(AlertSeverity.Critical);
        alert.Status.Should().Be(AlertStatus.Open);
        alert.Message.Should().Be("Device 'sw-01' está fora do ar (ping falhou).");
        alert.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithoutDeviceId_ShouldThrowArgumentErrorPtBr()
    {
        var action = () => Alert.Create(
            Guid.Empty, CheckKind.Ping, AlertSeverity.Critical, "msg");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id do device é obrigatório.*");
    }

    [Fact]
    public void Resolve_ShouldCloseAlertAndStampResolvedAt()
    {
        var alert = Alert.Create(
            Guid.NewGuid(), CheckKind.SnmpToner, AlertSeverity.Warning, "toner 18%");

        alert.Resolve();

        alert.Status.Should().Be(AlertStatus.Resolved);
        alert.ResolvedAt.Should().NotBeNull();

        // F19-T007: resolução levanta AlertResolvedDomainEvent (notificação
        // Operator + Admin pelo NotificationEventConsumer).
        var resolved = alert.DomainEvents.OfType<AlertResolvedDomainEvent>().Single();
        resolved.AlertId.Should().Be(alert.Id);
        resolved.Message.Should().Be("toner 18%");
    }

    [Fact]
    public void Resolve_Twice_ShouldKeepFirstStamp()
    {
        var alert = Alert.Create(
            Guid.NewGuid(), CheckKind.Ping, AlertSeverity.Critical, "down");
        alert.Resolve();
        var first = alert.ResolvedAt;

        alert.Resolve();

        alert.ResolvedAt.Should().Be(first);
        alert.DomainEvents.OfType<AlertResolvedDomainEvent>().Should().HaveCount(1);
    }

    // ── F6-T004: Suppressed (supressão topológica) ─────────────────────

    [Fact]
    public void Suppress_FromOpen_ShouldSetSuppressed()
    {
        var alert = Alert.Create(
            Guid.NewGuid(), CheckKind.Ping, AlertSeverity.Critical, "down");

        alert.Suppress();

        alert.Status.Should().Be(AlertStatus.Suppressed);
        alert.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void Suppress_FromResolved_ShouldStayResolved()
    {
        var alert = Alert.Create(
            Guid.NewGuid(), CheckKind.Ping, AlertSeverity.Critical, "down");
        alert.Resolve();

        alert.Suppress();

        alert.Status.Should().Be(AlertStatus.Resolved,
            "Resolved é definitivo — supressão não retrocede o histórico");
    }

    [Fact]
    public void Reopen_FromSuppressed_ShouldReturnOpen()
    {
        var alert = Alert.Create(
            Guid.NewGuid(), CheckKind.Ping, AlertSeverity.Critical, "down");
        alert.Suppress();

        alert.Reopen();

        alert.Status.Should().Be(AlertStatus.Open);
    }

    [Fact]
    public void Reopen_FromResolved_ShouldStayResolved()
    {
        var alert = Alert.Create(
            Guid.NewGuid(), CheckKind.Ping, AlertSeverity.Critical, "down");
        alert.Resolve();

        alert.Reopen();

        alert.Status.Should().Be(AlertStatus.Resolved,
            "upstream recuperado não reabre alerta já resolvido");
    }

    [Fact]
    public void Resolve_FromSuppressed_ShouldCloseWithStamp()
    {
        var alert = Alert.Create(
            Guid.NewGuid(), CheckKind.SnmpToner, AlertSeverity.Warning, "toner 18%");
        alert.Suppress();

        alert.Resolve();

        alert.Status.Should().Be(AlertStatus.Resolved);
        alert.ResolvedAt.Should().NotBeNull();

        // F19-T007: resolução levanta AlertResolvedDomainEvent (notificação
        // Operator + Admin pelo NotificationEventConsumer).
        var resolved = alert.DomainEvents.OfType<AlertResolvedDomainEvent>().Single();
        resolved.AlertId.Should().Be(alert.Id);
        resolved.Message.Should().Be("toner 18%");
    }

    [Fact]
    public void Suppress_ThenReopen_ShouldRoundTripWithoutResolveStamp()
    {
        var alert = Alert.Create(
            Guid.NewGuid(), CheckKind.Ping, AlertSeverity.Critical, "down");

        alert.Suppress();
        alert.Reopen();

        alert.Status.Should().Be(AlertStatus.Open);
        alert.ResolvedAt.Should().BeNull();
    }

    // ── F33-T004/ADR-059: AlertCreatedDomainEvent (só Critical) ──────

    [Fact]
    public void Create_Critical_ShouldRaiseAlertCreatedDomainEvent()
    {
        var device = Guid.NewGuid();
        var alert = Alert.Create(
            device, CheckKind.Ping, AlertSeverity.Critical,
            "Device 'sw-01' está fora do ar (ping falhou).");

        // Critical sempre garante incidente no mesmo fluxo (F6-T003).
        var created = alert.DomainEvents.OfType<AlertCreatedDomainEvent>().Single();
        created.AlertId.Should().Be(alert.Id);
        created.Severity.Should().Be(AlertSeverity.Critical);
        created.DeviceId.Should().Be(device);
        created.Message.Should().Be("Device 'sw-01' está fora do ar (ping falhou).");
        created.HasIncident.Should().BeTrue();
    }

    [Fact]
    public void Create_Warning_ShouldNotRaiseAlertCreatedDomainEvent()
    {
        var alert = Alert.Create(
            Guid.NewGuid(), CheckKind.Ping, AlertSeverity.Warning, "latência alta");

        alert.DomainEvents.OfType<AlertCreatedDomainEvent>().Should().BeEmpty(
            "só Critical notifica (decisão 2 do ADR-059: eventos = Critical)");
    }

    [Fact]
    public void CreateExternal_Critical_ShouldRaiseAlertCreatedDomainEvent()
    {
        var alert = Alert.CreateExternal(
            Guid.NewGuid(), "10451", Guid.NewGuid(), kind: null,
            AlertSeverity.Critical, "CPU em 98%", MonitoringSource.Zabbix);

        var created = alert.DomainEvents.OfType<AlertCreatedDomainEvent>().Single();
        created.AlertId.Should().Be(alert.Id);
        created.Severity.Should().Be(AlertSeverity.Critical);
        created.Message.Should().Be("CPU em 98%");
        created.HasIncident.Should().BeTrue(
            "externo Critical garante incidente no mesmo fluxo (ADR-027 §3)");
    }

    [Fact]
    public void CreateExternal_Warning_ShouldNotRaiseAlertCreatedDomainEvent()
    {
        var alert = Alert.CreateExternal(
            Guid.NewGuid(), "10452", Guid.NewGuid(), kind: null,
            AlertSeverity.Warning, "disco em 85%", MonitoringSource.Zabbix);

        alert.DomainEvents.OfType<AlertCreatedDomainEvent>().Should().BeEmpty();
    }

    [Fact]
    public void Suppress_AtBirth_ShouldRemovePendingAlertCreatedDomainEvent()
    {
        var alert = Alert.Create(
            Guid.NewGuid(), CheckKind.Ping, AlertSeverity.Critical, "down");

        // Vítima de incidente upstream (F6-T004) — nasce Suppressed.
        alert.Suppress();

        alert.Status.Should().Be(AlertStatus.Suppressed);
        alert.DomainEvents.OfType<AlertCreatedDomainEvent>().Should().BeEmpty(
            "o grupo já recebe o Incidente aberto do incidente raiz — sem duplicar");
    }
}

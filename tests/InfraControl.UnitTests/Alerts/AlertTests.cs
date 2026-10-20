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

    // ── F33-T004/ADR-059: AlertCreatedDomainEvent (só Critical) ───────

    [Fact]
    public void Create_Critical_ShouldRaiseAlertCreatedWithFields()
    {
        var device = Guid.NewGuid();
        var alert = Alert.Create(
            device, CheckKind.Ping, AlertSeverity.Critical,
            "Device 'sw-01' está fora do ar (ping falhou).");

        var created = alert.DomainEvents.OfType<AlertCreatedDomainEvent>().Single();
        created.AlertId.Should().Be(alert.Id);
        created.Severity.Should().Be(AlertSeverity.Critical);
        created.DeviceId.Should().Be(device);
        created.Message.Should().Contain("sw-01");
        created.HasIncident.Should().BeFalse("o avaliador marca após garantir o vínculo");
        created.EventType.Should().Be(nameof(AlertCreatedDomainEvent));
    }

    [Fact]
    public void Create_Warning_ShouldNotRaiseAlertCreated()
    {
        var alert = Alert.Create(
            Guid.NewGuid(), CheckKind.SnmpToner, AlertSeverity.Warning, "toner 18%");

        alert.DomainEvents.OfType<AlertCreatedDomainEvent>().Should().BeEmpty(
            "Warning não vira notificação (ADR-059 §6 — volume mínimo)");
    }

    [Fact]
    public void CreateExternal_Critical_ShouldRaiseAlertCreated()
    {
        var alert = Alert.CreateExternal(
            Guid.NewGuid(), "ext-42", Guid.NewGuid(), kind: null,
            AlertSeverity.Critical, "Host 'db-01' sem resposta.",
            MonitoringSource.Zabbix);

        var created = alert.DomainEvents.OfType<AlertCreatedDomainEvent>().Single();
        created.AlertId.Should().Be(alert.Id);
        created.Severity.Should().Be(AlertSeverity.Critical);
        created.Message.Should().Contain("db-01");
    }

    [Fact]
    public void CreateExternal_Warning_ShouldNotRaiseAlertCreated()
    {
        var alert = Alert.CreateExternal(
            Guid.NewGuid(), "ext-43", Guid.NewGuid(), kind: null,
            AlertSeverity.Warning, "CPU 75%.",
            MonitoringSource.Zabbix);

        alert.DomainEvents.OfType<AlertCreatedDomainEvent>().Should().BeEmpty();
    }

    [Fact]
    public void Suppress_WithPendingAlertCreated_ShouldDropIt()
    {
        var alert = Alert.Create(
            Guid.NewGuid(), CheckKind.Ping, AlertSeverity.Critical, "down");

        alert.Suppress();

        alert.DomainEvents.OfType<AlertCreatedDomainEvent>().Should().BeEmpty(
            "suprimido não notifica 'Alerta criado' — o incidente upstream já avisou");
        alert.Status.Should().Be(AlertStatus.Suppressed);
    }

    [Fact]
    public void SetHasIncident_ShouldMarkPendingEventOnly()
    {
        var alert = Alert.Create(
            Guid.NewGuid(), CheckKind.Ping, AlertSeverity.Critical, "down");

        alert.SetHasIncident();

        alert.DomainEvents.OfType<AlertCreatedDomainEvent>().Single()
            .HasIncident.Should().BeTrue();

        // No-op quando não há evento pendente (alerta já existente).
        var warning = Alert.Create(
            Guid.NewGuid(), CheckKind.Ping, AlertSeverity.Warning, "cpu 70%");
        var action = () => warning.SetHasIncident();

        action.Should().NotThrow();
        warning.DomainEvents.OfType<AlertCreatedDomainEvent>().Should().BeEmpty();
    }
}

using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

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
}

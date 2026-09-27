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
}

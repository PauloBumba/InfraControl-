using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F5-T001: MonitoringCheck — instância de check por device que nasce do
/// DeviceProfile (ADR-014 §4).
/// </summary>
public sealed class MonitoringCheckTests
{
    [Fact]
    public void Create_ShouldSetDeviceKindAndEnabledByDefault()
    {
        var deviceId = Guid.NewGuid();

        var check = MonitoringCheck.Create(deviceId, CheckKind.Ping);

        check.Id.Should().NotBe(Guid.Empty);
        check.DeviceId.Should().Be(deviceId);
        check.Kind.Should().Be(CheckKind.Ping);
        check.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Create_EmptyDeviceId_ShouldThrowArgumentPtBrMessage()
    {
        var action = () => MonitoringCheck.Create(Guid.Empty, CheckKind.Ping);

        var exception = action.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().StartWith("O id do device é obrigatório.");
    }
}

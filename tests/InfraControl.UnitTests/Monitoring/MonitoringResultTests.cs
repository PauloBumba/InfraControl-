using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F5-T002: MonitoringResult — histórico de polls persistido.
/// </summary>
public sealed class MonitoringResultTests
{
    [Fact]
    public void Create_ShouldSetFields()
    {
        var deviceId = Guid.NewGuid();
        var values = new Dictionary<string, string> { ["levelPercent"] = "42" };

        var result = MonitoringResult.Create(
            deviceId, CheckKind.SnmpToner, succeeded: false, durationMs: 250,
            error: "Falha ao consultar SNMP: timeout.", values: values);

        result.Id.Should().NotBe(Guid.Empty);
        result.DeviceId.Should().Be(deviceId);
        result.Kind.Should().Be(CheckKind.SnmpToner);
        result.Succeeded.Should().BeFalse();
        result.DurationMs.Should().Be(250);
        result.Error.Should().Be("Falha ao consultar SNMP: timeout.");
        result.Values.Should().BeEquivalentTo(values);
    }

    [Fact]
    public void Create_EmptyDeviceId_ShouldThrowArgumentPtBrMessage()
    {
        var action = () => MonitoringResult.Create(
            Guid.Empty, CheckKind.Ping, succeeded: true, durationMs: 10);

        var exception = action.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().StartWith("O id do device é obrigatório.");
    }

    [Fact]
    public void Create_NegativeDuration_ShouldThrowArgumentPtBrMessage()
    {
        var action = () => MonitoringResult.Create(
            Guid.NewGuid(), CheckKind.Ping, succeeded: true, durationMs: -1);

        var exception = action.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().StartWith("A duração do poll não pode ser negativa.");
    }

    [Fact]
    public void Create_ErrorLongerThan500_ShouldTruncate()
    {
        var error = new string('x', 600);

        var result = MonitoringResult.Create(
            Guid.NewGuid(), CheckKind.Ping, succeeded: false, durationMs: 1, error: error);

        result.Error.Should().HaveLength(500);
    }
}

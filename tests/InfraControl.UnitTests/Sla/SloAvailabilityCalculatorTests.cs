using FluentAssertions;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.Application.Sla.Slos.Bindings;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Sla;

/// <summary>
/// ADR-068: cálculo puro da disponibilidade do SLO — % de sucesso dos
/// pares (DeviceId, Kind) vinculados; sem poll na janela ⇒ null.
/// </summary>
public sealed class SloAvailabilityCalculatorTests
{
    private static readonly Guid DeviceA = Guid.NewGuid();
    private static readonly Guid DeviceB = Guid.NewGuid();

    [Fact]
    public void ComputePercent_ShouldAverageSuccessAcrossBoundPairs()
    {
        var pairs = new[] { (DeviceA, CheckKind.Ping), (DeviceB, CheckKind.HttpWan) };
        var counts = new[]
        {
            new DeviceKindPollCount(DeviceA, CheckKind.Ping, Total: 100, Succeeded: 99),
            new DeviceKindPollCount(DeviceB, CheckKind.HttpWan, Total: 50, Succeeded: 40),
            // Par fora do vínculo — não pode entrar na conta.
            new DeviceKindPollCount(DeviceA, CheckKind.Tcp, Total: 999, Succeeded: 0),
        };

        // (99+40)/(100+50) = 92.666… → 92.667
        SloAvailabilityCalculator.ComputePercent(pairs, counts).Should().Be(92.667m);
    }

    [Fact]
    public void ComputePercent_NoPollsInWindow_ShouldReturnNull()
    {
        var pairs = new[] { (DeviceA, CheckKind.Ping) };
        var counts = new[] { new DeviceKindPollCount(DeviceA, CheckKind.Tcp, 10, 10) };

        SloAvailabilityCalculator.ComputePercent(pairs, counts).Should().BeNull();
    }

    [Fact]
    public void ComputePercent_NoBoundPairs_ShouldReturnNull()
    {
        SloAvailabilityCalculator.ComputePercent(
            [],
            [new DeviceKindPollCount(DeviceA, CheckKind.Ping, 10, 10)])
            .Should().BeNull();
    }

    [Fact]
    public void ComputePercent_AllSucceeded_ShouldReturn100()
    {
        var pairs = new[] { (DeviceA, CheckKind.Ping) };
        var counts = new[] { new DeviceKindPollCount(DeviceA, CheckKind.Ping, 30, 30) };

        SloAvailabilityCalculator.ComputePercent(pairs, counts).Should().Be(100m);
    }
}

using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F5-T003: PingMonitoringProvider (chave "Ping") com seam IIcmpClient —
/// testado sem rede real.
/// </summary>
public sealed class PingMonitoringProviderTests
{
    private sealed class FakeIcmpClient(IcmpReply reply) : IIcmpClient
    {
        public int Calls { get; private set; }

        public Task<IcmpReply> SendAsync(
            string target,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(reply);
        }
    }

    [Fact]
    public void Name_ShouldBePing()
        => new PingMonitoringProvider(new FakeIcmpClient(new IcmpReply(true, 1, null)))
            .Name.Should().Be("Ping");

    [Theory]
    [InlineData("Ping")]
    [InlineData("ping")]
    public void CanHandle_PingKey_ShouldBeTrue(string key)
        => new PingMonitoringProvider(new FakeIcmpClient(new IcmpReply(true, 1, null)))
            .CanHandle(key).Should().BeTrue();

    [Theory]
    [InlineData("SnmpToner")]
    [InlineData("HttpWan")]
    [InlineData("")]
    public void CanHandle_OtherKey_ShouldBeFalse(string key)
        => new PingMonitoringProvider(new FakeIcmpClient(new IcmpReply(true, 1, null)))
            .CanHandle(key).Should().BeFalse();

    [Fact]
    public async Task CheckAsync_Success_ShouldReturnSucceededWithDuration()
    {
        var icmp = new FakeIcmpClient(new IcmpReply(true, 5, null));
        var provider = new PingMonitoringProvider(icmp);

        var result = await provider.CheckAsync("192.0.2.10");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        icmp.Calls.Should().Be(1);
    }

    [Fact]
    public async Task CheckAsync_Failure_ShouldReturnFailedWithPtBrError()
    {
        var provider = new PingMonitoringProvider(
            new FakeIcmpClient(new IcmpReply(false, 0, "Host inacessível.")));

        var result = await provider.CheckAsync("192.0.2.99");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Host inacessível.");
    }

    [Fact]
    public async Task CheckAsync_EmptyTarget_ShouldFailWithoutCallingIcmp()
    {
        var icmp = new FakeIcmpClient(new IcmpReply(true, 1, null));
        var provider = new PingMonitoringProvider(icmp);

        var result = await provider.CheckAsync("  ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino do ping é obrigatório.");
        icmp.Calls.Should().Be(0, "não deve disparar ICMP sem destino");
    }
}

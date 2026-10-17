using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T006b: YealinkCallQualityProvider — qualidade de chamada
/// (MOS/jitter/latência/perda, docs/47 §D) + PoE (§E) sobre a seam
/// IYealinkPhoneClient (HTTP/JSON mockado).
/// </summary>
public sealed class YealinkCallQualityProviderTests
{
    private sealed class FakeYealinkClient(
        Func<string, YealinkCallQualityInfo?> qualityFactory,
        Func<string, YealinkPoeInfo?>? poeFactory = null) : IYealinkPhoneClient
    {
        public List<string> AskedQuality { get; } = [];
        public List<string> AskedPoe { get; } = [];

        public Task<YealinkSipInfo?> GetSipInfoAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.FromResult<YealinkSipInfo?>(null);

        public Task<YealinkCallQualityInfo?> GetCallQualityAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            AskedQuality.Add(target);
            return Task.FromResult(qualityFactory(target));
        }

        public Task<YealinkPoeInfo?> GetPoeStatusAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            AskedPoe.Add(target);
            return Task.FromResult(poeFactory?.Invoke(target));
        }
    }

    private sealed class ThrowingQualityClient : IYealinkPhoneClient
    {
        public Task<YealinkSipInfo?> GetSipInfoAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => throw new TimeoutException("Yealink timeout simulado.");

        public Task<YealinkCallQualityInfo?> GetCallQualityAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => throw new TimeoutException("Yealink timeout simulado.");

        public Task<YealinkPoeInfo?> GetPoeStatusAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.FromResult<YealinkPoeInfo?>(null);
    }

    [Fact]
    public void NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new YealinkCallQualityProvider(
            new FakeYealinkClient(_ => new YealinkCallQualityInfo(4.2m, 8m, 30m, 0.5m)));

        provider.Name.Should().Be("YealinkCallQuality");
        provider.CanHandle("YealinkCallQuality").Should().BeTrue();
        provider.CanHandle("yealinkcallquality").Should().BeTrue();
        provider.CanHandle("YealinkSip").Should().BeFalse();
    }

    [Fact]
    public async Task Success_ShouldReportAllMetricsAndPoE()
    {
        var provider = new YealinkCallQualityProvider(new FakeYealinkClient(
            _ => new YealinkCallQualityInfo(4.2m, 8.5m, 32.0m, 1.25m),
            _ => new YealinkPoeInfo(true, 3.1m)));

        var result = await provider.CheckAsync("192.0.2.90");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Values!["mos"].Should().Be("4.2");
        result.Values["jitterMs"].Should().Be("8.5");
        result.Values["latencyMs"].Should().Be("32.0");
        result.Values["packetLossPercent"].Should().Be("1.3");
        result.Values["poePowered"].Should().Be("true");
        result.Values["poePowerW"].Should().Be("3.1");
    }

    [Fact]
    public async Task NoCallActiveButPoEAvailable_ShouldSucceedWithPoEOnly()
    {
        var provider = new YealinkCallQualityProvider(new FakeYealinkClient(
            _ => null,
            _ => new YealinkPoeInfo(true, 2.8m)));

        var result = await provider.CheckAsync("192.0.2.91");

        result.Succeeded.Should().BeTrue();
        result.Values!.Should().NotContainKey("mos");
        result.Values["poePowered"].Should().Be("true");
        result.Values["poePowerW"].Should().Be("2.8");
    }

    [Fact]
    public async Task QualityButPoEUnsupported_ShouldSucceedWithQualityOnly()
    {
        var provider = new YealinkCallQualityProvider(new FakeYealinkClient(
            _ => new YealinkCallQualityInfo(3.4m, null, null, null),
            _ => null));

        var result = await provider.CheckAsync("192.0.2.92");

        result.Succeeded.Should().BeTrue();
        result.Values!["mos"].Should().Be("3.4");
        result.Values.Should().NotContainKey("poePowered");
    }

    [Fact]
    public async Task PartialMetrics_ShouldReportOnlyPresentFields()
    {
        var provider = new YealinkCallQualityProvider(new FakeYealinkClient(
            _ => new YealinkCallQualityInfo(null, 60.0m, null, null)));

        var result = await provider.CheckAsync("192.0.2.93");

        result.Succeeded.Should().BeTrue();
        result.Values!.Keys.Should().Equal("jitterMs");
    }

    [Fact]
    public async Task NothingAvailable_ShouldFailWithPtBrMessage()
    {
        var provider = new YealinkCallQualityProvider(new FakeYealinkClient(
            _ => null, _ => null));

        var result = await provider.CheckAsync("192.0.2.94");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(
            "Qualidade de chamada via API Yealink indisponível "
            + "(sem chamada ativa ou modelo não suportado).");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFailWithoutCallingClient()
    {
        var client = new FakeYealinkClient(_ => null);
        var provider = new YealinkCallQualityProvider(client);

        var result = await provider.CheckAsync(" ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino da API Yealink é obrigatório.");
        client.AskedQuality.Should().BeEmpty();
    }

    [Fact]
    public async Task ClientThrow_ShouldFailWithPtBrTransportMessage()
    {
        var provider = new YealinkCallQualityProvider(new ThrowingQualityClient());

        var result = await provider.CheckAsync("192.0.2.95");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar API Yealink:")
            .And.Contain("Yealink timeout simulado.");
    }

    [Fact]
    public async Task InvariantCulture_ShouldFormatDecimalPoint()
    {
        var provider = new YealinkCallQualityProvider(new FakeYealinkClient(
            _ => new YealinkCallQualityInfo(4.5m, 10.5m, 45.5m, 2.5m),
            _ => new YealinkPoeInfo(false, 0.0m)));

        var result = await provider.CheckAsync("192.0.2.96");

        result.Values!["mos"].Should().Be("4.5");
        result.Values["jitterMs"].Should().Be("10.5");
        result.Values["packetLossPercent"].Should().Be("2.5");
        result.Values["poePowered"].Should().Be("false");
        result.Values["poePowerW"].Should().Be("0.0");
    }
}

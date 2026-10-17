using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T006a: YealinkSipProvider — registro SIP do telefone via API
/// Yealink (docs/47 §C) sobre a seam IYealinkPhoneClient (HTTP/JSON
/// mockado — mesmo molde dos testes de OnvifStorageProvider).
/// </summary>
public sealed class YealinkSipProviderTests
{
    private sealed class FakeYealinkClient(
        Func<string, YealinkSipInfo?> sipFactory) : IYealinkPhoneClient
    {
        public List<string> AskedSip { get; } = [];

        public Task<YealinkSipInfo?> GetSipInfoAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            AskedSip.Add(target);
            return Task.FromResult(sipFactory(target));
        }

        public Task<YealinkCallQualityInfo?> GetCallQualityAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.FromResult<YealinkCallQualityInfo?>(null);

        public Task<YealinkPoeInfo?> GetPoeStatusAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.FromResult<YealinkPoeInfo?>(null);
    }

    private sealed class ThrowingYealinkClient : IYealinkPhoneClient
    {
        public Task<YealinkSipInfo?> GetSipInfoAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => throw new TimeoutException("Yealink timeout simulado.");

        public Task<YealinkCallQualityInfo?> GetCallQualityAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => throw new TimeoutException("Yealink timeout simulado.");

        public Task<YealinkPoeInfo?> GetPoeStatusAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => throw new TimeoutException("Yealink timeout simulado.");
    }

    [Fact]
    public void NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new YealinkSipProvider(
            new FakeYealinkClient(_ => new YealinkSipInfo(true, "1001", "pbx.local", "G.711")));

        provider.Name.Should().Be("YealinkSip");
        provider.CanHandle("YealinkSip").Should().BeTrue();
        provider.CanHandle("yealinksip").Should().BeTrue();
        provider.CanHandle("SnmpSystem").Should().BeFalse();
    }

    [Fact]
    public async Task Success_ShouldReportAllFields()
    {
        var provider = new YealinkSipProvider(new FakeYealinkClient(
            _ => new YealinkSipInfo(true, "1001", "pbx.local", "G.722")));

        var result = await provider.CheckAsync("192.0.2.60");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Values!["sipRegistered"].Should().Be("true");
        result.Values["extension"].Should().Be("1001");
        result.Values["sipServer"].Should().Be("pbx.local");
        result.Values["codec"].Should().Be("G.722");
    }

    [Fact]
    public async Task Unregistered_ShouldReportFalse()
    {
        var provider = new YealinkSipProvider(new FakeYealinkClient(
            _ => new YealinkSipInfo(false, "1002", null, null)));

        var result = await provider.CheckAsync("192.0.2.61");

        result.Succeeded.Should().BeTrue();
        result.Values!["sipRegistered"].Should().Be("false");
        result.Values["extension"].Should().Be("1002");
        result.Values.Should().NotContainKey("sipServer");
        result.Values.Should().NotContainKey("codec");
    }

    [Fact]
    public async Task UnknownStatusOnly_ShouldStillSucceedWithPresentFields()
    {
        var provider = new YealinkSipProvider(new FakeYealinkClient(
            _ => new YealinkSipInfo(null, "1003", "pbx.local", null)));

        var result = await provider.CheckAsync("192.0.2.62");

        result.Succeeded.Should().BeTrue();
        result.Values!.Should().NotContainKey("sipRegistered");
        result.Values["extension"].Should().Be("1003");
        result.Values["sipServer"].Should().Be("pbx.local");
    }

    [Fact]
    public async Task AllNulls_ShouldFailWithPtBrUnsupportedMessage()
    {
        var provider = new YealinkSipProvider(new FakeYealinkClient(
            _ => new YealinkSipInfo(null, null, null, null)));

        var result = await provider.CheckAsync("192.0.2.63");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Registro SIP via API Yealink indisponível neste modelo.");
    }

    [Fact]
    public async Task NullFromClient_ShouldFailWithPtBrUnsupportedMessage()
    {
        var provider = new YealinkSipProvider(new FakeYealinkClient(_ => null));

        var result = await provider.CheckAsync("192.0.2.64");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Registro SIP via API Yealink indisponível neste modelo.");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFailWithoutCallingClient()
    {
        var client = new FakeYealinkClient(_ => null);
        var provider = new YealinkSipProvider(client);

        var result = await provider.CheckAsync("  ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino da API Yealink é obrigatório.");
        client.AskedSip.Should().BeEmpty();
    }

    [Fact]
    public async Task ClientThrow_ShouldFailWithPtBrTransportMessage()
    {
        var provider = new YealinkSipProvider(new ThrowingYealinkClient());

        var result = await provider.CheckAsync("192.0.2.65");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar API Yealink:")
            .And.Contain("Yealink timeout simulado.");
    }

    [Fact]
    public async Task Target_ShouldBeTrimmedBeforeCall()
    {
        var client = new FakeYealinkClient(
            _ => new YealinkSipInfo(true, "1001", null, null));
        var provider = new YealinkSipProvider(client);

        await provider.CheckAsync("  192.0.2.66  ");

        client.AskedSip.Should().Equal("192.0.2.66");
    }
}

using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T005a: OnvifStorageProvider — armazenamento/retenção da câmera/NVR
/// via ONVIF (docs/46 §C) sobre a seam IOnvifClient (SOAP mockado — mesmo
/// molde dos testes de provider SNMP com ISnmpClient fake).
/// </summary>
public sealed class OnvifStorageProviderTests
{
    private const long OneMb = 1024L * 1024L;

    private sealed class FakeOnvifClient(
        Func<string, OnvifStorageInfo?> storageFactory,
        Func<string, OnvifDeviceInformation?>? deviceFactory = null) : IOnvifClient
    {
        public List<string> AskedStorage { get; } = [];

        public Task<OnvifStorageInfo?> GetStorageAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            AskedStorage.Add(target);
            return Task.FromResult(storageFactory(target));
        }

        public Task<OnvifDeviceInformation?> GetDeviceInformationAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.FromResult(deviceFactory?.Invoke(target));

        public Task<OnvifRecordingStatus?> GetRecordingStatusAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.FromResult<OnvifRecordingStatus?>(null);
    }

    private sealed class ThrowingOnvifClient : IOnvifClient
    {
        public Task<OnvifStorageInfo?> GetStorageAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => throw new TimeoutException("ONVIF timeout simulado.");

        public Task<OnvifDeviceInformation?> GetDeviceInformationAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => throw new TimeoutException("ONVIF timeout simulado.");

        public Task<OnvifRecordingStatus?> GetRecordingStatusAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => throw new TimeoutException("ONVIF timeout simulado.");
    }

    private static OnvifStorageInfo Storage(
        long totalMb, long freeMb, int? retentionDays = null)
        => new(totalMb * OneMb, freeMb * OneMb, retentionDays);

    [Fact]
    public void NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new OnvifStorageProvider(
            new FakeOnvifClient(_ => Storage(100, 50)));

        provider.Name.Should().Be("OnvifStorage");
        provider.CanHandle("OnvifStorage").Should().BeTrue();
        provider.CanHandle("onvifstorage").Should().BeTrue();
        provider.CanHandle("SnmpSystem").Should().BeFalse();
    }

    [Fact]
    public async Task Success_ShouldReportTotalsUsedPercentAndRetention()
    {
        var provider = new OnvifStorageProvider(new FakeOnvifClient(
            _ => Storage(totalMb: 102_400, freeMb: 20_480, retentionDays: 30),
            _ => new OnvifDeviceInformation("Hikvision", "DS-2CD2143G2", "V5.7.3")));

        var result = await provider.CheckAsync("192.0.2.40");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Values!["totalMB"].Should().Be("102400");
        result.Values["availableMB"].Should().Be("20480");
        result.Values["usedMB"].Should().Be("81920");
        result.Values["usedPercent"].Should().Be("80");
        result.Values["retentionDays"].Should().Be("30");
        // best-effort de identificação (ficha §A) — não é o core do check
        result.Values["manufacturer"].Should().Be("Hikvision");
        result.Values["model"].Should().Be("DS-2CD2143G2");
        result.Values["firmware"].Should().Be("V5.7.3");
    }

    [Fact]
    public async Task DeviceInformationFailure_ShouldNotFailStorageCheck()
    {
        var provider = new OnvifStorageProvider(new FakeOnvifClient(
            _ => Storage(100, 25),
            _ => throw new TimeoutException("device info indisponível")));

        var result = await provider.CheckAsync("192.0.2.41");

        result.Succeeded.Should().BeTrue("device info é best-effort");
        result.Values!["usedPercent"].Should().Be("75");
        result.Values.Should().NotContainKey("manufacturer");
    }

    [Fact]
    public async Task PercentRounding_ShouldUseAwayFromZero()
    {
        var provider = new OnvifStorageProvider(new FakeOnvifClient(
            _ => new OnvifStorageInfo(300, 199, null)));

        var result = await provider.CheckAsync("192.0.2.42");

        result.Succeeded.Should().BeTrue();
        result.Values!["usedPercent"].Should().Be("34", "101/300 = 33,66% arredonda para 34");
    }

    [Fact]
    public async Task NullStorage_ShouldFailWithNotSupportedMessage()
    {
        var provider = new OnvifStorageProvider(new FakeOnvifClient(_ => null));

        var result = await provider.CheckAsync("192.0.2.43");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("Armazenamento ONVIF indisponível neste modelo.");
        result.Values.Should().BeNull();
    }

    [Fact]
    public async Task ZeroCapacity_ShouldFailWithPtBrMessage()
    {
        var provider = new OnvifStorageProvider(new FakeOnvifClient(
            _ => new OnvifStorageInfo(0, 0, null)));

        var result = await provider.CheckAsync("192.0.2.44");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("capacidade zero");
    }

    [Fact]
    public async Task ClientThrow_ShouldFailWithPtBrMessage()
    {
        var provider = new OnvifStorageProvider(new ThrowingOnvifClient());

        var result = await provider.CheckAsync("192.0.2.45");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar ONVIF:")
            .And.Contain("ONVIF timeout simulado.");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFailWithPtBrMessage()
    {
        var provider = new OnvifStorageProvider(
            new FakeOnvifClient(_ => Storage(100, 50)));

        var result = await provider.CheckAsync("  ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino do ONVIF é obrigatório.");
        result.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task ShouldAskStorageWithTrimmedTarget()
    {
        var client = new FakeOnvifClient(_ => Storage(100, 50));
        var provider = new OnvifStorageProvider(client);

        await provider.CheckAsync("  192.0.2.46 ");

        client.AskedStorage.Should().Equal("192.0.2.46");
    }
}

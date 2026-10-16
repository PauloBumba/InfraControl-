using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T005b: OnvifRecordingProvider — estado de gravação/video loss/tamper
/// da câmera/NVR via ONVIF (docs/46 §B/C) sobre a seam IOnvifClient.
/// </summary>
public sealed class OnvifRecordingProviderTests
{
    private sealed class FakeOnvifClient(
        Func<string, OnvifRecordingStatus?> statusFactory) : IOnvifClient
    {
        public Task<OnvifStorageInfo?> GetStorageAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.FromResult<OnvifStorageInfo?>(null);

        public Task<OnvifDeviceInformation?> GetDeviceInformationAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.FromResult<OnvifDeviceInformation?>(null);

        public Task<OnvifRecordingStatus?> GetRecordingStatusAsync(
            string target, TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.FromResult(statusFactory(target));
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

    [Fact]
    public void NameAndKeys_ShouldMatchCheckKind()
    {
        var provider = new OnvifRecordingProvider(
            new FakeOnvifClient(_ => new OnvifRecordingStatus(true, false, false)));

        provider.Name.Should().Be("OnvifRecording");
        provider.CanHandle("OnvifRecording").Should().BeTrue();
        provider.CanHandle("onvifrecording").Should().BeTrue();
        provider.CanHandle("OnvifStorage").Should().BeFalse();
    }

    [Fact]
    public async Task Success_ShouldReportAllThreeStatesAsStrings()
    {
        var provider = new OnvifRecordingProvider(new FakeOnvifClient(
            _ => new OnvifRecordingStatus(RecordingActive: false, VideoLoss: true, Tamper: false)));

        var result = await provider.CheckAsync("192.0.2.60");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Values!["recordingActive"].Should().Be("false");
        result.Values["videoLoss"].Should().Be("true");
        result.Values["tamper"].Should().Be("false");
    }

    [Fact]
    public async Task PartialStatus_ShouldReportOnlyAvailableFields()
    {
        var provider = new OnvifRecordingProvider(new FakeOnvifClient(
            _ => new OnvifRecordingStatus(RecordingActive: null, VideoLoss: false, Tamper: null)));

        var result = await provider.CheckAsync("192.0.2.61");

        result.Succeeded.Should().BeTrue();
        result.Values!.Should().ContainKey("videoLoss").WhoseValue.Should().Be("false");
        result.Values.Should().NotContainKey("recordingActive");
        result.Values.Should().NotContainKey("tamper");
    }

    [Fact]
    public async Task NullStatus_ShouldFailWithNotSupportedMessage()
    {
        var provider = new OnvifRecordingProvider(new FakeOnvifClient(_ => null));

        var result = await provider.CheckAsync("192.0.2.62");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("Estado de gravação ONVIF indisponível neste modelo.");
    }

    [Fact]
    public async Task ClientThrow_ShouldFailWithPtBrMessage()
    {
        var provider = new OnvifRecordingProvider(new ThrowingOnvifClient());

        var result = await provider.CheckAsync("192.0.2.63");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar ONVIF:")
            .And.Contain("ONVIF timeout simulado.");
    }

    [Fact]
    public async Task EmptyTarget_ShouldFailWithPtBrMessage()
    {
        var provider = new OnvifRecordingProvider(
            new FakeOnvifClient(_ => new OnvifRecordingStatus(true, false, false)));

        var result = await provider.CheckAsync(" ");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino do ONVIF é obrigatório.");
        result.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task AllNullFields_ShouldFailAsUnsupported()
    {
        var provider = new OnvifRecordingProvider(new FakeOnvifClient(
            _ => new OnvifRecordingStatus(null, null, null)));

        var result = await provider.CheckAsync("192.0.2.64");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("indisponível neste modelo");
    }
}

using System.Net.Sockets;
using FluentAssertions;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F5-T006: provider TCP do monitoring (CheckKind.Tcp) — porta fixa
/// 37777 (default de câmera), escopo aprovado pelo usuário.
/// </summary>
public sealed class TcpMonitoringProviderTests
{
    private sealed class FakeTcpProbeClient(Func<TcpProbeReply> factory) : ITcpProbeClient
    {
        public int? LastPort { get; private set; }
        public string? LastTarget { get; private set; }

        public Task<TcpProbeReply> SendAsync(
            string target,
            int port,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            LastTarget = target;
            LastPort = port;
            return Task.FromResult(factory());
        }
    }

    private sealed class ThrowingTcpProbeClient : ITcpProbeClient
    {
        public Task<TcpProbeReply> SendAsync(
            string target,
            int port,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
            => throw new SocketException(111);
    }

    [Fact]
    public void Name_ShouldBeTcp()
        => new TcpMonitoringProvider(new FakeTcpProbeClient(() => default))
            .Name.Should().Be("Tcp");

    [Theory]
    [InlineData("Tcp")]
    [InlineData("tcp")]
    [InlineData("TCP")]
    public void CanHandle_TcpKey_ShouldBeTrue(string key)
        => new TcpMonitoringProvider(new FakeTcpProbeClient(() => default))
            .CanHandle(key).Should().BeTrue();

    [Theory]
    [InlineData("Ping")]
    [InlineData("HttpWan")]
    [InlineData("")]
    public void CanHandle_OtherKey_ShouldBeFalse(string key)
        => new TcpMonitoringProvider(new FakeTcpProbeClient(() => default))
            .CanHandle(key).Should().BeFalse();

    [Fact]
    public async Task CheckAsync_PortOpen_ShouldSucceedWithPortValue()
    {
        var client = new FakeTcpProbeClient(() => new TcpProbeReply(true));
        var provider = new TcpMonitoringProvider(client);

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Values!["port"].Should().Be("37777");
        client.LastTarget.Should().Be("192.0.2.1");
        client.LastPort.Should().Be(37777);
    }

    [Fact]
    public async Task CheckAsync_PortClosed_ShouldFailWithPtBrErrorAndDetail()
    {
        var client = new FakeTcpProbeClient(
            () => new TcpProbeReply(false, "Connection refused"));
        var provider = new TcpMonitoringProvider(client);

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Porta 37777 inacessível em 192.0.2.1.");
        result.Values!["tcp_error"].Should().Be("Connection refused");
    }

    [Fact]
    public async Task CheckAsync_EmptyTarget_ShouldFailWithoutCallingProbe()
    {
        var client = new FakeTcpProbeClient(() => new TcpProbeReply(true));
        var provider = new TcpMonitoringProvider(client);

        var result = await provider.CheckAsync("");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Destino do TCP é obrigatório.");
        client.LastPort.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_ProbeThrows_ShouldFailWithPtBrError()
    {
        var provider = new TcpMonitoringProvider(new ThrowingTcpProbeClient());

        var result = await provider.CheckAsync("192.0.2.1");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Falha ao consultar TCP:");
    }
}

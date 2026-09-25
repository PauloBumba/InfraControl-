using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using InfraControl.Application.RemoteAccess.Sessions.Ports;
using InfraControl.Infrastructure.RemoteAccess;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.RemoteAccess;

/// <summary>
/// F11-T003: RdpRemoteProvider faz probe TCP/X.224 — conecta na porta RDP e
/// completa a negociação de conexão para confirmar que o endpoint fala RDP,
/// sem autenticação. Servidor fake em loopback simula confirmação, recusa da
/// negociação, resposta que não é RDP e silêncio — nada de rede externa.
/// </summary>
public sealed class RdpRemoteProviderTests
{
    /// <summary>X.224 Connection Confirm + RDP Negotiation Response (SSL).</summary>
    private static readonly byte[] ConnectionConfirm =
    [
        0x03, 0x00, 0x00, 0x13, 0x0E, 0xD0, 0x00, 0x00, 0x00, 0x01, 0x00,
        0x02, 0x00, 0x08, 0x00, 0x01, 0x00, 0x00, 0x00
    ];

    /// <summary>X.224 CC + RDP Negotiation Failure (HYBRID_REQUIRED_BY_SERVER).</summary>
    private static readonly byte[] NegotiationFailureNla =
    [
        0x03, 0x00, 0x00, 0x13, 0x0E, 0xD0, 0x00, 0x00, 0x00, 0x01, 0x00,
        0x03, 0x00, 0x08, 0x00, 0x04, 0x00, 0x00, 0x00
    ];

    /// <summary>Resposta que não começa com TPKT — endpoint não é RDP.</summary>
    private static readonly byte[] NotRdpResponse =
        "HTTP/1.1 400 Bad Request\r\n\r\n"u8.ToArray();

    private static RdpRemoteProvider CreateProvider(int port, int timeoutSeconds = 3)
        => new(Options.Create(new RemoteRdpOptions
        {
            Port = port,
            TimeoutSeconds = timeoutSeconds
        }));

    private static int GetClosedLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Theory]
    [InlineData("rdp")]
    [InlineData("Rdp")]
    [InlineData("RDP")]
    public void CanHandle_RdpKeys_ShouldReturnTrue(string providerKey)
        => CreateProvider(3389).CanHandle(providerKey).Should().BeTrue();

    [Theory]
    [InlineData("ssh")]
    [InlineData("ping")]
    [InlineData("")]
    public void CanHandle_OtherKeys_ShouldReturnFalse(string providerKey)
        => CreateProvider(3389).CanHandle(providerKey).Should().BeFalse();

    [Fact]
    public void Name_ShouldBeRdp()
        => CreateProvider(3389).Name.Should().Be("Rdp");

    [Fact]
    public async Task ConnectAsync_ConnectionConfirm_ShouldSucceedWithOpenConnection()
    {
        using var server = FakeRdpServer.RespondWith(ConnectionConfirm);
        var provider = CreateProvider(server.Port);

        var result = await provider.ConnectAsync(
            "127.0.0.1", new RemoteCredential(string.Empty, string.Empty));

        result.Succeeded.Should().BeTrue(
            "o CC com Negotiation Response confirma endpoint RDP");
        result.Error.Should().BeNull();
        result.Connection.Should().BeOfType<TcpClient>(
            "o socket TCP deve ficar registrado como conexão da sessão");
        result.Connection!.Dispose();
    }

    [Fact]
    public async Task ConnectAsync_NegotiationFailure_ShouldReturnNlaFailurePtBr()
    {
        using var server = FakeRdpServer.RespondWith(NegotiationFailureNla);
        var provider = CreateProvider(server.Port);

        var result = await provider.ConnectAsync(
            "127.0.0.1", new RemoteCredential(string.Empty, string.Empty));

        result.Succeeded.Should().BeFalse();
        result.Connection.Should().BeNull();
        result.Error.Should().Contain("recusou a negociação");
        result.Error.Should().Contain("NLA", "o código 4 é HYBRID_REQUIRED_BY_SERVER");
    }

    [Fact]
    public async Task ConnectAsync_NotRdpEndpoint_ShouldReturnFailurePtBr()
    {
        using var server = FakeRdpServer.RespondWith(NotRdpResponse);
        var provider = CreateProvider(server.Port);

        var result = await provider.ConnectAsync(
            "127.0.0.1", new RemoteCredential(string.Empty, string.Empty));

        result.Succeeded.Should().BeFalse();
        result.Connection.Should().BeNull();
        result.Error.Should().Contain("não respondeu como RDP");
    }

    [Fact]
    public async Task ConnectAsync_ClosedPort_ShouldReturnFailureResultWithoutThrowing()
    {
        var port = GetClosedLoopbackPort();
        var provider = CreateProvider(port);

        var result = await provider.ConnectAsync(
            "127.0.0.1", new RemoteCredential(string.Empty, string.Empty));

        result.Succeeded.Should().BeFalse();
        result.Connection.Should().BeNull();
        result.Error.Should().NotBeNullOrWhiteSpace();
        result.Error.Should().Contain("RDP", "a mensagem deve contextualizar em pt-BR");
    }

    [Fact]
    public async Task ConnectAsync_ServerAcceptsButSilent_ShouldTimeoutWithPtBrMessage()
    {
        using var server = FakeRdpServer.Silent();
        var provider = CreateProvider(server.Port, timeoutSeconds: 1);

        var result = await provider.ConnectAsync(
            "127.0.0.1", new RemoteCredential(string.Empty, string.Empty));

        result.Succeeded.Should().BeFalse();
        result.Connection.Should().BeNull();
        result.Error.Should().Contain("tempo esgotado");
        result.Error.Should().Contain("RDP");
    }

    [Fact]
    public async Task ConnectAsync_CancelledToken_ShouldThrowOperationCanceled()
    {
        var provider = CreateProvider(GetClosedLoopbackPort());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => provider.ConnectAsync(
            "127.0.0.1", new RemoteCredential(string.Empty, string.Empty), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>(
            "cancelamento do chamador não vira falha capturada");
    }

    /// <summary>
    /// Servidor RDP fake em loopback: aceita uma conexão, responde (ou fica
    /// em silêncio) e mantém o socket vivo até o Dispose do teste.
    /// </summary>
    private sealed class FakeRdpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;

        public int Port { get; }

        private FakeRdpServer(byte[]? response)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _loop = Task.Run(async () =>
            {
                try
                {
                    using var client = await _listener.AcceptTcpClientAsync();
                    if (response is not null)
                        await client.GetStream().WriteAsync(response);
                    await Task.Delay(Timeout.InfiniteTimeSpan, _cts.Token);
                }
                catch (OperationCanceledException) { }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
                catch (IOException) { }
            });
        }

        public static FakeRdpServer RespondWith(byte[] response) => new(response);

        public static FakeRdpServer Silent() => new(null);

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            try
            {
                _loop.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
                // encerramento forçado pelo Stop() já é esperado
            }
            _cts.Dispose();
        }
    }
}

using InfraControl.Application.RemoteAccess.Sessions.Ports;

namespace InfraControl.IntegrationTests.Common;

/// <summary>
/// Provider RDP falso para integration tests (F11-T003): simula probe bem-
/// sucedido sem abrir conexão de rede.
/// </summary>
public sealed class FakeRdpRemoteProvider : IRemoteProvider
{
    public string Name => "Rdp";

    public bool CanHandle(string providerKey) =>
        string.Equals(providerKey, "rdp", StringComparison.OrdinalIgnoreCase);

    public Task<RemoteConnectResult> ConnectAsync(
        string host,
        RemoteCredential credential,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new RemoteConnectResult(
            true, new FakeRdpConnection(host), null));
}

public sealed class FakeRdpConnection(string host) : IDisposable
{
    public string Host { get; } = host;
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}

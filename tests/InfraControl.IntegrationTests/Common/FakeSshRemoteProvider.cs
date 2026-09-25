using InfraControl.Application.RemoteAccess.Sessions.Ports;

namespace InfraControl.IntegrationTests.Common;

/// <summary>
/// SSH determinístico para integration tests — nenhum teste abre conexão
/// de rede (F11-T002, docs/10).
/// </summary>
public sealed class FakeSshRemoteProvider : IRemoteProvider
{
    public string Name => "SSH";

    public bool CanHandle(string providerKey) =>
        string.Equals(providerKey, "ssh", StringComparison.OrdinalIgnoreCase);

    public Task<RemoteConnectResult> ConnectAsync(
        string host,
        RemoteCredential credential,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new RemoteConnectResult(
            true, new FakeSshConnection(host), null));
}

public sealed class FakeSshConnection(string host) : IDisposable
{
    public string Host { get; } = host;
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}

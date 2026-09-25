using System.Collections.Concurrent;
using InfraControl.Application.RemoteAccess.Sessions.Ports;

namespace InfraControl.Infrastructure.RemoteAccess;

/// <summary>
/// Conexões de acesso remoto abertas em memória, indexadas por sessão
/// (F11-T002). Singleton: sobrevive aos scopes de request.
/// </summary>
public sealed class RemoteConnectionRegistry : IRemoteConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, IDisposable> _connections = new();

    public void Register(Guid sessionId, IDisposable connection) =>
        _connections[sessionId] = connection;

    public bool TryDisconnect(Guid sessionId)
    {
        if (!_connections.TryRemove(sessionId, out var connection))
            return false;

        connection.Dispose();
        return true;
    }
}

namespace InfraControl.Application.RemoteAccess.Sessions.Ports;

/// <summary>
/// Registro em memória das conexões de acesso remoto abertas, indexadas
/// pela sessão. Encerrar a sessão descarta a conexão correspondente.
/// </summary>
public interface IRemoteConnectionRegistry
{
    void Register(Guid sessionId, IDisposable connection);

    /// <summary>Remove e descarta a conexão; false se não havia registro.</summary>
    bool TryDisconnect(Guid sessionId);
}

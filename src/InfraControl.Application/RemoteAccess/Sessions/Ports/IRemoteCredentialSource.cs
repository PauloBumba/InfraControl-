namespace InfraControl.Application.RemoteAccess.Sessions.Ports;

/// <summary>
/// Fornece a credencial de acesso remoto de um device. Implementação
/// provisória lê configuração (F11-T002); F11-T006 troca por Credential Store.
/// </summary>
public interface IRemoteCredentialSource
{
    Task<RemoteCredential> GetCredentialAsync(
        Guid deviceId, CancellationToken cancellationToken = default);
}

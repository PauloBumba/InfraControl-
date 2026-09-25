using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Application.RemoteAccess.Sessions.Ports;

namespace InfraControl.Infrastructure.RemoteAccess;

/// <summary>
/// Cadeia de credencial (F11-T006, docs/10): store por device primeiro
/// (cifrado no banco); sem credencial no store, cai no fallback global
/// (RemoteAccess:Ssh via <see cref="ConfigurationRemoteCredentialSource"/>),
/// que mantém o erro pt-BR quando nada está configurado.
/// </summary>
public sealed class StoreRemoteCredentialSource(
    IDeviceCredentialRepository credentialRepository,
    ICredentialProtector protector,
    ConfigurationRemoteCredentialSource fallback)
    : IRemoteCredentialSource
{
    public async Task<RemoteCredential> GetCredentialAsync(
        Guid deviceId, CancellationToken cancellationToken = default)
    {
        var credential = await credentialRepository.GetByDeviceIdAsync(deviceId, cancellationToken);
        return credential is null
            ? await fallback.GetCredentialAsync(deviceId, cancellationToken)
            : new RemoteCredential(
                credential.Username, protector.Unprotect(credential.ProtectedPassword));
    }
}

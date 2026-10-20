using InfraControl.Domain.RemoteAccess;

namespace InfraControl.Application.RemoteAccess.Sessions.Ports;

/// <summary>
/// Gateway Guacamole do acesso remoto (ADR-060 D1/D2, F34-T007/T008). Minta
/// um token de sessão via header auth (server-to-server: a plataforma
/// envia <c>X-Remote-User</c> direto ao container do gateway — o nginx
/// limpa o header de toda requisição externa, anti-spoofing).
/// </summary>
public interface IGuacamoleGateway
{
    Task<string> CreateTokenAsync(
        string userName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Garante que a conexão do device exista no gateway e que
    /// <paramref name="grantToUser"/> tenha permissão de leitura sobre ela
    /// (F34-T008): cria/valida via REST do Guacamole com a credencial de
    /// administração do próprio gateway — o valor da credencial do ALVO
    /// (dono da conexão) nunca trafega para o navegador (D4); operações são
    /// idempotentes (grant só quando falta).
    /// </summary>
    Task EnsureDeviceConnectionAsync(
        string deviceName,
        string hostname,
        RemoteProtocol protocol,
        RemoteCredential credential,
        string grantToUser,
        CancellationToken cancellationToken = default);
}

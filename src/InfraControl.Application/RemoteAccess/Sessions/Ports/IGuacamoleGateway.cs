namespace InfraControl.Application.RemoteAccess.Sessions.Ports;

/// <summary>
/// Gateway Guacamole do acesso remoto (ADR-060 D1/D2, F34-T007). Minta
/// um token de sessão via header auth (server-to-server: a plataforma
/// envia <c>X-Remote-User</c> direto ao container do gateway — o nginx
/// limpa o header de toda requisição externa, anti-spoofing).
/// </summary>
public interface IGuacamoleGateway
{
    Task<string> CreateTokenAsync(
        string userName, CancellationToken cancellationToken = default);
}

using InfraControl.Application.Common.Interfaces;

namespace InfraControl.Application.RemoteAccess.Sessions.Ports;

/// <summary>
/// Credencial de acesso remoto. Transportada apenas em memória — nunca
/// persistida nem exposta em DTO (docs/10, F11-T006 substitui por Credential Store).
/// </summary>
public sealed record RemoteCredential(string Username, string Password);

/// <summary>Resultado de uma tentativa de conexão de acesso remoto.</summary>
public sealed record RemoteConnectResult(
    bool Succeeded,
    IDisposable? Connection,
    string? Error = null);

/// <summary>Provider de acesso remoto (SSH em F11-T002, RDP em F11-T003).</summary>
public interface IRemoteProvider : IInfrastructureProvider
{
    Task<RemoteConnectResult> ConnectAsync(
        string host,
        RemoteCredential credential,
        CancellationToken cancellationToken = default);
}

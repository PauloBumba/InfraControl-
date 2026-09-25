namespace InfraControl.Infrastructure.RemoteAccess;

/// <summary>
/// Credencial SSH provisória via configuração/env (F11-T002, ADR-013).
/// F11-T006 substitui por Credential Store — nada de segredos no git.
/// </summary>
public sealed class RemoteSshOptions
{
    public const string SectionName = "RemoteAccess:Ssh";

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public int TimeoutSeconds { get; set; } = 10;
}

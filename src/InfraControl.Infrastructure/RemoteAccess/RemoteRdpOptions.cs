namespace InfraControl.Infrastructure.RemoteAccess;

/// <summary>
/// Configuração do probe RDP (F11-T003, ADR-013): porta e timeout da
/// negociação X.224. Sem credenciais — o probe não autentica (NLA/CredSSP
/// fica para task futura; nada de segredos no git).
/// </summary>
public sealed class RemoteRdpOptions
{
    public const string SectionName = "RemoteAccess:Rdp";

    public int Port { get; set; } = 3389;
    public int TimeoutSeconds { get; set; } = 10;
}

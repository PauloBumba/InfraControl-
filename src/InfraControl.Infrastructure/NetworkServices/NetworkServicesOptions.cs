namespace InfraControl.Infrastructure.NetworkServices;

/// <summary>
/// F18-T005 (ADR-042 §1): alvo e credencial de serviço dos serviços de
/// rede. `Target` vazio = integração desligada (endpoints 503 + motivo);
/// a senha vem de env (`NetworkServices__SshPassword`), nunca do banco.
/// </summary>
public sealed class NetworkServicesOptions
{
    public const string SectionName = "NetworkServices";

    /// <summary>IP do equipamento de rede consultado (switch/router/fw).</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Driver de firewall (comando + parser): v1 = "cisco".</summary>
    public string Vendor { get; set; } = "cisco";

    public string SshUsername { get; set; } = string.Empty;

    public string SshPassword { get; set; } = string.Empty;

    public int SshPort { get; set; } = 22;

    public int TimeoutSeconds { get; set; } = 5;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Target);
}

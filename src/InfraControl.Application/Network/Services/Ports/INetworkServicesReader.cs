namespace InfraControl.Application.Network.Services.Ports;

/// <summary>
/// Porta de leitura dos serviços de rede (F18-T005 · ADR-042) —
/// implementação em Infrastructure (SNMP para leases/rotas, SSH por
/// vendor para ACLs). Somente leitura: nenhum método desta porta escreve
/// no equipamento.
/// </summary>
public interface INetworkServicesReader
{
    /// <summary>Leases/ARP ativos (ipNetToMedia/ipNetToPhysical).</summary>
    Task<IReadOnlyList<DhcpLeaseDto>> GetDhcpLeasesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Tabela de rotas IP (ipRouteTable).</summary>
    Task<IReadOnlyList<NetworkRouteDto>> GetRoutesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Regras de firewall (ACLs) via comando show por vendor.</summary>
    Task<IReadOnlyList<FirewallRuleDto>> GetFirewallRulesAsync(
        CancellationToken cancellationToken = default);
}

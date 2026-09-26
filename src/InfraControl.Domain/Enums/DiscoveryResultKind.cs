namespace InfraControl.Domain.Enums;

/// <summary>
/// Tipo genérico de achado de discovery (F4-T002). Aprovado pelo usuário
/// (Kind + Summary + JSON) para cobrir todos os providers sem nova
/// migração: vizinhos (LLDP/CDP), ARP, MAC tables, DHCP e hosts (ICMP).
/// </summary>
public enum DiscoveryResultKind
{
    Neighbor,
    ArpEntry,
    MacTableEntry,
    DhcpLease,
    Host
}

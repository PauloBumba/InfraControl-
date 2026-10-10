namespace InfraControl.Application.Network.Services;

/// <summary>
/// Lease/ARP ativo lido do equipamento (F18-T005 · ADR-042): walk de
/// <c>1.3.6.1.2.1.4.22.1</c> — ip = endereço ativo (ipNetToMedia/
/// ipNetToPhysical), mac normalizado <c>aa:bb:cc:dd:ee:ff</c> quando o
/// device devolve hex, ifIndex = interface onde o host foi visto.
/// Snapshot ao vivo — nada é persistido.
/// </summary>
public sealed record DhcpLeaseDto(string Ip, string Mac, int IfIndex);

/// <summary>
/// Rota da tabela IP (F18-T005 · ADR-042): walk de
/// <c>1.3.6.1.2.1.4.21.1</c> (ipRouteTable, RFC1213). <c>Type</c> e
/// <c>Protocol</c> vêm com os rótulos do RFC1213 quando conhecidos;
/// número fora do mapa fica cru (nunca rotulado por suposição).
/// </summary>
public sealed record NetworkRouteDto(
    string Destination,
    string Mask,
    string NextHop,
    int IfIndex,
    int Metric,
    string Type,
    string Protocol);

/// <summary>
/// Regra de ACL lida via SSH de leitura (F18-T005 · ADR-042):
/// <c>Expression</c> é o texto CRU do device (host/any/wildcard/eq…)
/// — o parser não inventa semântica, só separa cabeçalho do ACL,
/// sequência, ação e protocolo.
/// </summary>
public sealed record FirewallRuleDto(
    string Acl,
    string Scope,
    long Sequence,
    string Action,
    string Protocol,
    string Expression);

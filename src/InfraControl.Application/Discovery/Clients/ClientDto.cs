namespace InfraControl.Application.Discovery.Clients;

/// <summary>
/// Cliente de rede devolvido pelo agregador de ARP/DHCP de todas as
/// execuções de discovery (F18-T008). Dedupe por MAC normalizado (fallback =
/// IP quando a linha não tem MAC); `LastSeenAt` é o `CreatedAt` mais recente
/// do grupo e os campos ausentes da linha mais nova vêm das mais antigas do
/// mesmo grupo. Pelo menos um entre `Ip`/`Mac` é sempre preenchido — linhas
/// sem os dois (achados negativos, ex.: "Nenhuma entrada ARP.") ficam de fora.
/// </summary>
public sealed record ClientDto(
    string? Ip,
    string? Mac,
    string? Hostname,
    string? Vlan,
    string? LocalIf,
    string Kind,
    DateTime LastSeenAt);

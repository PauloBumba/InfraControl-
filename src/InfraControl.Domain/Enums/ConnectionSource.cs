namespace InfraControl.Domain.Enums;

/// <summary>
/// Origem da informação da Connection (F53-T001, spec 11.7): como
/// aprendemos que o link existe. Manual = CRUD do administrador
/// (spec 11.3); Lldp/Cdp = ingestão automática do discovery;
/// Unifi = reservado — o UniFi é indicação live, sem persistir
/// (REGRA 8/ADR-028); Unknown = linhas legadas sem origem conhecida
/// (não inventar ligação).
/// </summary>
public enum ConnectionSource
{
    Unknown = 0,
    Manual = 1,
    Lldp = 2,
    Cdp = 3,
    Unifi = 4
}

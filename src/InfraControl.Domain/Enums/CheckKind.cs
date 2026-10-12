namespace InfraControl.Domain.Enums;

/// <summary>
/// Tipos de check suportados pelo DeviceProfile (ADR-014). A execução em si
/// (providers ICMP/SNMP/HTTP) é responsabilidade de F5 — aqui só o que monitorar.
/// </summary>
public enum CheckKind
{
    Ping = 0,
    SnmpToner = 1,
    SnmpDrum = 2,
    HttpWan = 3,
    SnmpSystem = 4,
    Tcp = 5,

    /// <summary>
    /// F29-3: saúde das interfaces (ifTable operStatus/erros + ifXTable
    /// ifHCIn/OutOctets) — agregado por device. Disponível mas NÃO é default
    /// de nenhum tipo (DeviceProfileDefaults intocado): habilitar por device
    /// quando o SNMP community estiver liberado.
    /// </summary>
    SnmpInterfaces = 6
}

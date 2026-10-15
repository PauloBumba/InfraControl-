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
    SnmpInterfaces = 6,

    /// <summary>
    /// F30-T002a: toner colorido C/M/Y (Printer MIB prtMarkerSuppliesTable).
    /// Não é default do Printer (opt-in por device quando o community SNMP
    /// estiver liberado) — monocromático continua sendo o SnmpToner.
    /// </summary>
    SnmpColorToner = 7,

    /// <summary>
    /// F30-T002b: estado da impressora — bandejas (prtInputStatus), tampa
    /// (prtCoverStatus) e alertas (prtAlertTable) via Printer MIB. Opt-in,
    /// não é default do Printer.
    /// </summary>
    SnmpPrinterStatus = 8,

    /// <summary>
    /// F30-T002c: contadores de uso — total de páginas via prtMarkerLifeCount
    /// (counter monotônico). Informativo: NÃO abre alerta; volume mensal é
    /// diff entre checks (histórico fica no Zabbix — REGRA 8 ADR-028).
    /// Opt-in, não é default do Printer.
    /// </summary>
    SnmpPrinterCounters = 9,

    /// <summary>
    /// F30-T001 (docs/42 §C): saúde do switch (CPU/memória/temperatura),
    /// escolhendo os OIDs por <c>Device.Vendor</c>. Disponível mas NÃO é
    /// default de nenhum tipo (DeviceProfileDefaults intocado): habilitar por
    /// device quando o SNMP community estiver liberado — mesmo gate do
    /// SnmpInterfaces.
    ///
    /// REPARO 2026-10-15 (fusão das sessões): era 7 e colidia com
    /// SnmpColorToner — o ToString() do enum virava "SnmpColorToner", o
    /// Name do provider errava e o guard Printer-only disparava para Switch.
    /// Reatribuído a 10 (próximo livre): o kind é persistido como string e
    /// nenhum teste afirma valor numérico — sem migração, sem dado perdido.
    /// </summary>
    SnmpHealth = 10,

    /// <summary>
    /// F30-T004 S2 (ficha 45 §C/D/E, docs/52 §S2): fallback SNMP do Access
    /// Point pela IEEE 802.11 MIB (dot11: estações/SSID/canal/potência/
    /// RSSI/privacidade por ifIndex). Opt-in por device — nenhum tipo tem
    /// como default (DeviceProfileDefaults intocado); guard AccessPoint-only
    /// no RunCheckCommandHandler (mesmo molde do guard Printer).
    /// </summary>
    SnmpRadio = 11

}

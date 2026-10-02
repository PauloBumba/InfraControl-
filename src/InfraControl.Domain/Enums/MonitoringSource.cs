namespace InfraControl.Domain.Enums;

/// <summary>
/// Origem do alerta/evento (ADR-027): <see cref="Native"/> = pipeline interno
/// (checks ICMP/SNMP/HTTP/TCP); <see cref="Zabbix"/> = ingestão webhook
/// (ADR-023). Guardado como string para estender sem migração de enum.
/// </summary>
public enum MonitoringSource
{
    Native = 0,
    Zabbix = 1
}

namespace InfraControl.Domain.Enums;

/// <summary>
/// Fonte do identificador externo estável (F19-T006): <see cref="UniFi"/>
/// (MAC/Device ID — F20) e <see cref="Zabbix"/> (hostid). Guardado como
/// string para estender sem migração de enum. A resolução do Zabbix por
/// IP/hostname NÃO muda (regra 6 da revisão F19/F20/F21): a referência só
/// entra quando a fonte manda um identificador estável.
/// </summary>
public enum ExternalDeviceSource
{
    UniFi = 1,
    Zabbix = 2
}

namespace InfraControl.Application.Integrations.Zabbix.Ports;

/// <summary>
/// F27 Fase 2.2: opções Zabbix vindas de env/config (fallback quando não
/// existe linha no banco). Implementação em Infrastructure envolve
/// <c>IOptions&lt;ZabbixIntegrationOptions&gt;</c> para o Application não
/// depender de Options.
/// </summary>
public interface IZabbixApiOptionsAccessor
{
    ZabbixIntegrationOptions Current { get; }
}

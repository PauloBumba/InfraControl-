using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Integrations.Zabbix;

/// <summary>
/// F27 Fase 2.2: opções Zabbix de env/config (fallback) expostas para o
/// Application via porta.
/// </summary>
public sealed class ZabbixApiOptionsAccessor(IOptions<ZabbixIntegrationOptions> options)
    : IZabbixApiOptionsAccessor
{
    public ZabbixIntegrationOptions Current => options.Value;
}

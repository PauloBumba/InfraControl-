namespace InfraControl.Application.Integrations.Zabbix.Ports;

public interface IZabbixEventContextReader
{
    Task<ZabbixEventContextDto?> GetAsync(
        string eventId, CancellationToken cancellationToken = default);
}

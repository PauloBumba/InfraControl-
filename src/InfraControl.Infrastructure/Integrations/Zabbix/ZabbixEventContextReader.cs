using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Integrations.Zabbix;

public sealed class ZabbixEventContextReader(
    IZabbixClient client,
    ILogger<ZabbixEventContextReader> logger) : IZabbixEventContextReader
{
    public async Task<ZabbixEventContextDto?> GetAsync(
        string eventId, CancellationToken cancellationToken = default)
    {
        if (!client.IsConfigured)
            return null;

        try
        {
            var context = await client.GetEventContextAsync(eventId, cancellationToken);
            if (context is null)
                logger.LogWarning(
                    "Evento Zabbix {EventId} não foi encontrado na API ao enriquecer o contexto.",
                    eventId);

            return context;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Falha ao enriquecer contexto do evento Zabbix {EventId}; a ingestão continuará sem os metadados.",
                eventId);
            return null;
        }
    }
}

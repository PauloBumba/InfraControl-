using System.Globalization;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Notifications.WhatsApp;
using InfraControl.Domain.Events;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Notifications;

/// <summary>
/// F33-T005/ADR-059 (spike: fonte da série = API history do Zabbix):
/// resolve Device → host Zabbix (nome, depois IP) → item numérico
/// preferencial (chave com "cpu"/"load"/"ping", senão o primeiro ativo)
/// → últimas 2h de histórico. Qualquer passo sem dado devolve
/// <c>null</c> — o canal degrada para texto (fallback obrigatório).
/// Nunca lança: erro de rede/Zabbix vira Warning + null (o envio do
/// evento não pode travar por causa do gráfico).
/// </summary>
public sealed class ZabbixWhatsAppChartFactory(
    IDeviceRepository deviceRepository,
    IZabbixClient zabbixClient,
    ILogger<ZabbixWhatsAppChartFactory> logger)
    : IWhatsAppChartFactory
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(2);
    private const int HistoryLimit = 240;

    public async Task<IncidentChartData?> TryCreateAsync(
        Guid? deviceId,
        string title,
        string eventType,
        DateTime eventAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (deviceId is not { } id || id == Guid.Empty)
            return null;

        if (!zabbixClient.IsConfigured)
            return null;

        try
        {
            var device = await deviceRepository.GetByIdAsync(id, cancellationToken);
            if (device is null)
                return null;

            var host = await FindHostAsync(device.Name, device.Ip, cancellationToken);
            if (host is null)
            {
                logger.LogDebug(
                    "Gráfico WhatsApp: host Zabbix não encontrado para device {Device} (id {DeviceId}).",
                    device.Name, id);
                return null;
            }

            var items = await zabbixClient.ListItemsAsync(host.HostId, cancellationToken);
            var item = items
                .Where(entry => entry.Enabled && !entry.Unsupported)
                .OrderBy(Preference)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (item is null)
                return null;

            var history = await zabbixClient.GetHistoryAsync(
                item.ItemId, eventAtUtc - Window, eventAtUtc,
                HistoryLimit, cancellationToken);

            var series = new List<ChartSeriesPoint>(history.Count);
            foreach (var value in history)
            {
                if (double.TryParse(value.Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var parsed))
                    series.Add(new ChartSeriesPoint(value.ClockUtc, parsed));
            }

            if (series.Count == 0)
                return null;

            series.Sort((left, right) => left.AtUtc.CompareTo(right.AtUtc));

            return new IncidentChartData(
                KindFor(eventType),
                title,
                DeviceLabel: device.Name,
                SeverityLabel: SeverityFromTitle(title),
                EventAtUtc: eventAtUtc,
                Series: series);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Gráfico WhatsApp: falha ao montar série do Zabbix para device {DeviceId} — envio segue em texto.",
                deviceId);
            return null;
        }
    }

    /// <summary>Itens de CPU/disponibilidade primeiro — série com mais sentido no card.</summary>
    private static int Preference(ZabbixItemDto item)
    {
        var key = item.Key.ToLowerInvariant();
        if (key.Contains("cpu") || key.Contains("processor"))
            return 0;
        if (key.Contains("load") || key.Contains("ping") || key.Contains("icmpping"))
            return 1;
        return 2;
    }

    private async Task<ZabbixHostDto?> FindHostAsync(
        string deviceName, string? deviceIp, CancellationToken cancellationToken)
    {
        var hosts = await zabbixClient.ListHostsAsync(cancellationToken);

        var byName = hosts.FirstOrDefault(host => host.Enabled
            && string.Equals(host.Name, deviceName, StringComparison.OrdinalIgnoreCase));
        if (byName is not null)
            return byName;

        byName = hosts.FirstOrDefault(host => host.Enabled
            && (host.Host.Contains(deviceName, StringComparison.OrdinalIgnoreCase)
                || deviceName.Contains(host.Host, StringComparison.OrdinalIgnoreCase)));
        if (byName is not null)
            return byName;

        if (string.IsNullOrWhiteSpace(deviceIp))
            return null;

        return hosts.FirstOrDefault(host => host.Enabled
            && host.Interfaces.Any(inf =>
                string.Equals(inf.Ip, deviceIp, StringComparison.OrdinalIgnoreCase)));
    }

    private static ChartCardKind KindFor(string eventType) => eventType switch
    {
        nameof(AlertResolvedDomainEvent) or nameof(IncidentResolvedDomainEvent)
            => ChartCardKind.Resolution,
        nameof(AlertCreatedDomainEvent) or nameof(IncidentCreatedDomainEvent)
            => ChartCardKind.Incident,
        _ => ChartCardKind.Warning
    };

    private static string SeverityFromTitle(string title)
    {
        foreach (var severity in new[] { "Critical", "High", "Warning", "Low", "OK" })
        {
            if (title.Contains(severity, StringComparison.OrdinalIgnoreCase))
                return severity;
        }

        return "—";
    }
}

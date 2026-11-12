using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Application.Notifications.WhatsApp;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Events;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Notifications;

/// <summary>
/// F33-T005/ADR-059: resolve Device → host Zabbix → item relacionado ao
/// título do alerta → últimas 2h de histórico. Sem correspondência segura
/// ou sem série, devolve null para o envio seguir em texto.
/// </summary>
public sealed class ZabbixWhatsAppChartFactory(
    IDeviceRepository deviceRepository,
    ILocationRepository locationRepository,
    IZabbixClient zabbixClient,
    ILogger<ZabbixWhatsAppChartFactory> logger)
    : IWhatsAppChartFactory
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(2);
    private static readonly HashSet<string> IgnoredWords = new(StringComparer.Ordinal)
    {
        "alert", "alerta", "created", "criado", "opened", "open", "aberto",
        "resolved", "resolvido", "resolution", "incidente", "incident",
        "critical", "high", "medium", "warning", "low", "ok", "the", "for",
        "com", "de", "da", "do", "das", "dos", "uma", "um", "foi",
        "esta", "no", "na", "por", "se", "e", "o", "a"
    };
    private const int HistoryLimit = 240;

    /// <summary>
    /// Alertas de disponibilidade ("fora do ar", "indisponível", ICMP) não
    /// casam item por palavra — o item certo é o de disponibilidade do host.
    /// </summary>
    private static readonly string[] AvailabilityHints =
    [
        "fora do ar", "indispon", "unreachable", "offline", "down",
        "sem resposta", "perda de ping", "icmpping", "host down"
    ];

    /// <summary>Ordem de preferência dos itens de disponibilidade (spike visual primeiro).</summary>
    private static readonly string[] AvailabilityKeys = ["icmppingloss", "icmpping", "icmppingsec"];

    public async Task<IncidentChartData?> TryCreateAsync(
        Guid? deviceId,
        string title,
        string message,
        string eventType,
        DateTime eventAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (deviceId is not { } id || id == Guid.Empty || !zabbixClient.IsConfigured)
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

            var alertTitle = DedupHostPrefix(ResolveAlertTitle(title, message));
            var alertWords = Words(alertTitle);
            var items = (await zabbixClient.ListItemsAsync(host.HostId, cancellationToken))
                .Where(item => item.Enabled && !item.Unsupported)
                .ToList();
            var item = ResolveItem(items, alertTitle, alertWords);
            if (item is null)
                return null;
            var history = await zabbixClient.GetHistoryAsync(
                item.ItemId, eventAtUtc - Window, eventAtUtc,
                HistoryLimit, cancellationToken);

            var series = new List<ChartSeriesPoint>(history.Count);
            foreach (var value in history)
            {
                if (double.TryParse(value.Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var parsed)
                    && double.IsFinite(parsed))
                {
                    series.Add(new ChartSeriesPoint(value.ClockUtc, parsed));
                }
            }

            if (series.Count == 0)
                return null;

            series.Sort((left, right) => left.AtUtc.CompareTo(right.AtUtc));
            var severity = SeverityFromText($"{alertTitle} {message} {title}");

            return new IncidentChartData(
                KindFor(eventType, severity),
                alertTitle,
                DeviceLabel: device.Name,
                SeverityLabel: severity,
                EventAtUtc: eventAtUtc,
                Series: series,
                Unit: item.Unit,
                LocationLabel: await ResolveLocationLabelAsync(device, cancellationToken));
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

    /// <summary>
    /// Item do gráfico: match único por palavras do título; sem match (ou
    /// empate), cai no item de disponibilidade do host quando o alerta é
    /// de "fora do ar"/indisponibilidade — caso dos telefones e de hosts
    /// monitorados por ICMP, cujo título não repete nome de item.
    /// </summary>
    private static ZabbixItemDto? ResolveItem(
        IReadOnlyList<ZabbixItemDto> items, string alertTitle, HashSet<string> alertWords)
    {
        var candidates = items
            .Select(item => new { Item = item, Score = MatchScore(alertWords, item) })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ToList();

        if (candidates.Count == 1
            || (candidates.Count > 1 && candidates[0].Score != candidates[1].Score))
        {
            return candidates[0].Item;
        }

        if (!IsAvailabilityAlert(alertTitle))
            return null;

        foreach (var key in AvailabilityKeys)
        {
            var availability = items.FirstOrDefault(item =>
                string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
            if (availability is not null)
                return availability;
        }

        return null;
    }

    private static bool IsAvailabilityAlert(string alertTitle)
    {
        var normalized = alertTitle.Normalize(NormalizationForm.FormD);
        var plain = new string(normalized
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character)
                != UnicodeCategory.NonSpacingMark)
            .ToArray()).ToLowerInvariant();
        return AvailabilityHints.Any(plain.Contains);
    }

    /// <summary>Mesma regra do dispatcher: "{HOST}: {HOST} resto" → "resto".</summary>
    private static string DedupHostPrefix(string subject)
    {
        var idx = subject.IndexOf(": ", StringComparison.Ordinal);
        if (idx <= 0)
            return subject;

        var prefix = subject[..idx];
        var rest = subject[(idx + 2)..];
        return rest.StartsWith(prefix + " ", StringComparison.Ordinal)
            ? rest
            : subject;
    }

    private async Task<string?> ResolveLocationLabelAsync(
        Device device, CancellationToken cancellationToken)
    {
        if (device.LocationId == Guid.Empty)
            return null;

        try
        {
            return (await locationRepository.GetByIdAsync(
                device.LocationId, cancellationToken))?.Name;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogDebug(exception,
                "Gráfico WhatsApp: falha ao resolver local do device {DeviceId}.", device.Id);
            return null;
        }
    }

    private static string ResolveAlertTitle(string title, string message)
    {
        var firstQuote = message.IndexOf('\'');
        var titleEnd = firstQuote < 0 ? -1 : message.IndexOf('\'', firstQuote + 1);
        return titleEnd > firstQuote
            ? message[(firstQuote + 1)..titleEnd]
            : title;
    }

    private static int MatchScore(HashSet<string> alertWords, ZabbixItemDto item)
    {
        if (alertWords.Count == 0)
            return 0;

        var itemWords = Words($"{item.Name} {item.Key}");
        return alertWords.Count(itemWords.Contains);
    }

    private static HashSet<string> Words(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var withoutDiacritics = new string(normalized
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character)
                != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return Regex.Split(withoutDiacritics.ToLowerInvariant(), "[^a-z0-9]+")
            .Where(word => word.Length >= 3 && !IgnoredWords.Contains(word))
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task<ZabbixHostDto?> FindHostAsync(
        string deviceName, string? deviceIp, CancellationToken cancellationToken)
    {
        var hosts = await zabbixClient.ListHostsAsync(cancellationToken);

        var byName = hosts.FirstOrDefault(host => host.Enabled
            && string.Equals(host.Name, deviceName, StringComparison.OrdinalIgnoreCase));
        if (byName is not null)
            return byName;

        var fuzzyMatches = hosts.Where(host => host.Enabled
                && (host.Host.Contains(deviceName, StringComparison.OrdinalIgnoreCase)
                    || host.Name.Contains(deviceName, StringComparison.OrdinalIgnoreCase)
                    || deviceName.Contains(host.Host, StringComparison.OrdinalIgnoreCase)
                    || deviceName.Contains(host.Name, StringComparison.OrdinalIgnoreCase)))
            .Take(2)
            .ToList();
        if (fuzzyMatches.Count == 1)
            return fuzzyMatches[0];

        if (string.IsNullOrWhiteSpace(deviceIp))
            return null;

        return hosts.FirstOrDefault(host => host.Enabled
            && host.Interfaces.Any(inf =>
                string.Equals(inf.Ip, deviceIp, StringComparison.OrdinalIgnoreCase)));
    }

    private static ChartCardKind KindFor(string eventType, string severity) => eventType switch
    {
        nameof(AlertResolvedDomainEvent) or nameof(IncidentResolvedDomainEvent)
            => ChartCardKind.Resolution,
        _ when severity is "Warning" or "Medium" or "Low" => ChartCardKind.Warning,
        _ => ChartCardKind.Incident
    };

    private static string SeverityFromText(string text)
    {
        foreach (var severity in new[] { "Critical", "High", "Medium", "Warning", "Low", "OK" })
        {
            if (text.Contains(severity, StringComparison.OrdinalIgnoreCase))
                return severity;
        }

        return "—";
    }
}

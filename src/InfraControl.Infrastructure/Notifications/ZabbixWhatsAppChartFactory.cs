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
        if (!zabbixClient.IsConfigured)
            return null;

        try
        {
            var device = deviceId is { } id && id != Guid.Empty
                ? await deviceRepository.GetByIdAsync(id, cancellationToken)
                : null;

            var host = device is not null
                ? await FindHostAsync(device.Name, device.Ip, cancellationToken)
                : null;

            // Fallback: alerta do Zabbix sem Device vinculado (DeviceId
            // null — ex.: host não cadastrado no inventário) ainda pode
            // gerar o cartão: o nome do host vem no prefixo "{HOST}: …"
            // do título/mensagem e casa direto no read-model zabbix_hosts.
            host ??= await FindHostByAlertAsync(title, message, cancellationToken);
            if (host is null)
            {
                logger.LogInformation(
                    "Gráfico WhatsApp: host Zabbix não encontrado (device {DeviceId}) — envio em texto.",
                    deviceId);
                return null;
            }

            var alertTitle = DedupHostPrefix(ResolveAlertTitle(title, message));
            var alertWords = Words(alertTitle);
            var items = (await zabbixClient.ListItemsAsync(host.HostId, cancellationToken))
                .Where(item => item.Enabled && !item.Unsupported)
                .ToList();
            var item = ResolveItem(items, alertTitle, alertWords);
            if (item is null)
            {
                logger.LogInformation(
                    "Gráfico WhatsApp: nenhum item Zabbix casou com '{AlertTitle}' no host {Host} — envio em texto.",
                    alertTitle, host.Name);
                return null;
            }
            // history.get sem o parâmetro `history` volta vazio no Zabbix
            // 8.0 — o item pode estar em history_uint (3) ou history
            // (0=float). Mesma estratégia do GetZabbixHostSeriesQuery:
            // consulta as duas tabelas e deduplica por clock.
            var history = new List<ZabbixHistoryValueDto>();
            foreach (var table in new[] { 3, 0 })
            {
                history.AddRange(await zabbixClient.GetHistoryBatchAsync(
                    [item.ItemId], table, eventAtUtc - Window, eventAtUtc,
                    HistoryLimit, cancellationToken));
            }

            var series = new List<ChartSeriesPoint>(history.Count);
            foreach (var value in history
                .GroupBy(point => point.ClockUtc)
                .Select(group => group.First()))
            {
                if (double.TryParse(value.Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var parsed)
                    && double.IsFinite(parsed))
                {
                    series.Add(new ChartSeriesPoint(value.ClockUtc, parsed));
                }
            }

            if (series.Count == 0)
            {
                logger.LogInformation(
                    "Gráfico WhatsApp: item {ItemKey} sem pontos nas 2h anteriores — envio em texto.",
                    item.Key);
                return null;
            }

            series.Sort((left, right) => left.AtUtc.CompareTo(right.AtUtc));
            var severity = SeverityFromText($"{alertTitle} {message} {title}");
            logger.LogInformation(
                "Gráfico WhatsApp: cartão montado — host {Host}, item {ItemKey}, {Points} pontos.",
                host.Name, item.Key, series.Count);

            return new IncidentChartData(
                KindFor(eventType, severity),
                alertTitle,
                DeviceLabel: device?.Name ?? host.Name,
                SeverityLabel: severity,
                EventAtUtc: eventAtUtc,
                Series: series,
                Unit: item.Unit,
                LocationLabel: device is null
                    ? null
                    : await ResolveLocationLabelAsync(device, cancellationToken));
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
    /// Nome do item pesa mais que a key: parâmetros da key como
    /// "evolution-api" vazam a palavra "api" e empatavam com o item
    /// "api latência" de verdade.
    /// </summary>
    private static ZabbixItemDto? ResolveItem(
        IReadOnlyList<ZabbixItemDto> items, string alertTitle, HashSet<string> alertWords)
    {
        var candidates = items
            .Select(item =>
            {
                var nameScore = alertWords.Count(Words(item.Name).Contains);
                var score = alertWords.Count(
                    Words($"{item.Name} {item.Key}").Contains);
                return (Item: item, NameScore: nameScore, Score: score);
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.NameScore)
            .ThenByDescending(candidate => candidate.Score)
            .ToList();

        if (candidates.Count > 0
            && (candidates.Count == 1
                || candidates[0].NameScore != candidates[1].NameScore
                || candidates[0].Score != candidates[1].Score))
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

    /// <summary>
    /// Resolve o host Zabbix direto do alerta quando não há Device: o
    /// nome do problema vem como "{HOST}: {trigger}" — o trecho antes do
    /// primeiro ": " é o nome do host. Match exato por name/host; senão
    /// um único contains (ambíguo = null → texto).
    /// </summary>
    private async Task<ZabbixHostDto?> FindHostByAlertAsync(
        string title, string message, CancellationToken cancellationToken)
    {
        var alertTitle = ResolveAlertTitle(title, message).Trim();
        var sep = alertTitle.IndexOf(": ", StringComparison.Ordinal);
        var candidate = (sep > 0 ? alertTitle[..sep] : alertTitle).Trim();
        if (candidate.Length < 3)
            return null;

        var hosts = await zabbixClient.ListHostsAsync(cancellationToken);
        var enabled = hosts.Where(host => host.Enabled).ToList();

        var exact = enabled.FirstOrDefault(host =>
            string.Equals(host.Name, candidate, StringComparison.OrdinalIgnoreCase)
            || string.Equals(host.Host, candidate, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        var fuzzy = enabled
            .Where(host =>
                host.Name.Contains(candidate, StringComparison.OrdinalIgnoreCase)
                || host.Host.Contains(candidate, StringComparison.OrdinalIgnoreCase)
                || candidate.Contains(host.Name, StringComparison.OrdinalIgnoreCase)
                || candidate.Contains(host.Host, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();
        return fuzzy.Count == 1 ? fuzzy[0] : null;
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

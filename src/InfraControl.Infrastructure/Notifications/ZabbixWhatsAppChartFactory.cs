using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Integrations.WhatsApp.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Application.Monitoring.Events.Ports;
using InfraControl.Application.Notifications.WhatsApp;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Notifications;

/// <summary>
/// F33-T005/ADR-059: resolve Device → host Zabbix → item relacionado ao
/// título do alerta → últimas 2h de histórico. Sem correspondência segura
/// ou sem série, devolve null para o envio seguir em texto.
/// ADR-069: enriquece o cartão com o threshold real da trigger (expressão
/// via trigger.get, triggerId do MonitoringProblemState.CorrelationKey) e
/// a duração do ciclo (OpenedAt→ResolvedAt do mesmo estado).
/// </summary>
public sealed class ZabbixWhatsAppChartFactory(
    IDeviceRepository deviceRepository,
    ILocationRepository locationRepository,
    IAlertRepository alertRepository,
    IZabbixClient zabbixClient,
    IMonitoringProblemStateRepository problemStateRepository,
    IWhatsAppSettingsRepository settingsRepository,
    IMonitoringEventRepository monitoringEventRepository,
    ILogger<ZabbixWhatsAppChartFactory> logger)
    : IWhatsAppChartFactory
{
    private const int DefaultWindowHours = 2;
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
        Guid? aggregateId = null,
        CancellationToken cancellationToken = default)
    {
        if (!zabbixClient.IsConfigured)
            return null;

        // ADR-069: tipo do cartão + settings antes de qualquer chamada ao
        // Zabbix — toggle desligado = null imediato (envio segue em texto).
        var severity = SeverityFromText($"{title} {message}");
        var kind = KindFor(eventType, severity);
        var cardSettings = await settingsRepository.GetAsync(cancellationToken);
        if (!IsCardEnabled(cardSettings, kind))
            return null;
        var window = TimeSpan.FromHours(
            cardSettings?.CardWindowHours ?? DefaultWindowHours);

        try
        {
            var device = deviceId is { } id && id != Guid.Empty
                ? await deviceRepository.GetByIdAsync(id, cancellationToken)
                : null;

            var alertTitle = DedupHostPrefix(ResolveAlertTitle(title, message));

            // F57: estado do ciclo da OCORRÊNCIA (não "o mais recente do
            // device" — um host pode ter vários problemas simultâneos).
            // Casa pelo nome do problema: alert.Message = nome Zabbix →
            // GetByAlertIdAsync acha o MonitoringProblemState exato
            // (triggerId/CorrelationKey + janela do ciclo). Sem casamento
            // exato, cai no mais recente do device (comportamento anterior).
            var problemState = await ResolveProblemStateAsync(
                deviceId, alertTitle, aggregateId, cancellationToken);

            // Resolução exata primeiro: aggregate_id da notification é o
            // Alert → MonitoringEvent → SourceContextJson.HostIds dá o
            // host Zabbix REAL do evento (sem adivinhar pelo título —
            // prefixos de template tipo "Linux:" não são host).
            var host = await ResolveHostByAggregateAsync(
                aggregateId, cancellationToken);

            host ??= device is not null
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

            var alertWords = Words(alertTitle);
            var limitValue = await ResolveLimitValueAsync(
                problemState, kind, cancellationToken);
            var cycleDuration = ResolveCycleDuration(problemState, kind, eventAtUtc);
            var locationLabel = device is null
                ? null
                : await ResolveLocationLabelAsync(device, cancellationToken);
            var hostLabel = device?.Ip
                ?? host.Interfaces.FirstOrDefault()?.Ip
                ?? host.Host;

            // Pedido do usuário: SEMPRE cartão, mesmo sem histórico —
            // host encontrado no Zabbix já enriquece device/IP/host no
            // PNG; a área do gráfico mostra "Sem dados históricos".
            IncidentChartData Partial(ZabbixItemDto? item) => new(
                kind,
                alertTitle,
                DeviceLabel: device?.Name ?? host.Name,
                SeverityLabel: severity,
                EventAtUtc: eventAtUtc,
                Series: [],
                LimitValue: limitValue,
                Unit: item?.Unit ?? "",
                LocationLabel: locationLabel,
                CycleDuration: cycleDuration,
                AccentColor: AccentColorFor(cardSettings, kind),
                DeviceIp: device?.Ip,
                DeviceTypeLabel: device is null ? null : DeviceTypeLabel(device.Type),
                HostLabel: hostLabel,
                MetricLabel: item is null
                    ? null
                    : string.IsNullOrWhiteSpace(item.Unit)
                        ? $"{item.Name} · {item.Key}"
                        : $"{item.Name} · {item.Key} · {item.Unit}");

            var items = (await zabbixClient.ListItemsAsync(host.HostId, cancellationToken))
                .Where(item => item.Enabled && !item.Unsupported)
                .ToList();
            var item = ResolveItem(items, alertTitle, alertWords);
            if (item is null)
            {
                logger.LogInformation(
                    "Gráfico WhatsApp: nenhum item Zabbix casou com '{AlertTitle}' no host {Host} — cartão sem série.",
                    alertTitle, host.Name);
                return Partial(null);
            }
            // history.get sem o parâmetro `history` volta vazio no Zabbix
            // 8.0 — o item pode estar em history_uint (3) ou history
            // (0=float). Mesma estratégia do GetZabbixHostSeriesQuery:
            // consulta as duas tabelas e deduplica por clock.
            var history = new List<ZabbixHistoryValueDto>();
            foreach (var table in new[] { 3, 0 })
            {
                history.AddRange(await zabbixClient.GetHistoryBatchAsync(
                    [item.ItemId], table, eventAtUtc - window, eventAtUtc,
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
                    "Gráfico WhatsApp: item {ItemKey} sem pontos nas 2h anteriores — cartão sem série.",
                    item.Key);
                return Partial(item);
            }

            series.Sort((left, right) => left.AtUtc.CompareTo(right.AtUtc));
            logger.LogInformation(
                "Gráfico WhatsApp: cartão montado — host {Host}, item {ItemKey}, {Points} pontos.",
                host.Name, item.Key, series.Count);

            return new IncidentChartData(
                kind,
                alertTitle,
                DeviceLabel: device?.Name ?? host.Name,
                SeverityLabel: severity,
                EventAtUtc: eventAtUtc,
                Series: series,
                LimitValue: limitValue,
                Unit: item.Unit,
                LocationLabel: locationLabel,
                CycleDuration: cycleDuration,
                AccentColor: AccentColorFor(cardSettings, kind),
                DeviceIp: device?.Ip,
                DeviceTypeLabel: device is null ? null : DeviceTypeLabel(device.Type),
                HostLabel: hostLabel,
                MetricLabel: string.IsNullOrWhiteSpace(item.Unit)
                    ? $"{item.Name} · {item.Key}"
                    : $"{item.Name} · {item.Key} · {item.Unit}");
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
    /// F57: estado do problema da ocorrência exata — primeiro pelo
    /// aggregateId (AlertId canônico da notificação); depois pelo alerta
    /// do device com a mensagem = nome do problema → MonitoringProblemState
    /// pelo AlertId do ciclo. Fallback: mais recente do device (cobre
    /// eventos sem alert correspondente, ex.: teste manual do cartão).
    /// </summary>
    private async Task<MonitoringProblemState?> ResolveProblemStateAsync(
        Guid? deviceId, string alertTitle, Guid? aggregateId,
        CancellationToken cancellationToken)
    {
        if (aggregateId is { } aggregate && aggregate != Guid.Empty)
        {
            var byAggregate = await problemStateRepository.GetByAlertIdAsync(
                aggregate, cancellationToken);
            if (byAggregate is not null)
                return byAggregate;
        }

        if (deviceId is not { } id || id == Guid.Empty)
            return null;

        if (!string.IsNullOrWhiteSpace(alertTitle))
        {
            var alert = await alertRepository.GetLatestByDeviceAndMessageAsync(
                id, alertTitle, cancellationToken);
            if (alert is not null)
            {
                var state = await problemStateRepository.GetByAlertIdAsync(
                    alert.Id, cancellationToken);
                if (state is not null)
                    return state;
            }
        }

        return await problemStateRepository.GetLatestByDeviceAsync(
            MonitoringSource.Zabbix, id, cancellationToken);
    }

    /// <summary>Rótulo pt-BR do tipo do equipamento (mesmos nomes do frontend).</summary>
    private static string DeviceTypeLabel(DeviceType type) => type switch
    {
        DeviceType.Server => "Servidor",
        DeviceType.Workstation => "Estação de Trabalho",
        DeviceType.Switch => "Switch",
        DeviceType.Router => "Roteador",
        DeviceType.Firewall => "Firewall",
        DeviceType.AccessPoint => "Access Point",
        DeviceType.Printer => "Impressora",
        DeviceType.Camera => "Câmera",
        DeviceType.Phone => "Telefone",
        DeviceType.UPS => "UPS",
        DeviceType.Nvr => "NVR",
        DeviceType.VirtualMachine => "Máquina Virtual",
        DeviceType.VCenter => "vCenter",
        _ => "Outro",
    };

    /// <summary>
    /// Item do gráfico: match único por palavras do título; sem match (ou
    /// empate), cai no item de disponibilidade do host quando o alerta é
    /// de "fora do ar"/indisponibilidade — caso dos telefones e de hosts
    /// monitorados por ICMP, cujo título não repete nome de item.
    /// Nome do item pesa mais que a key: parâmetros da key como
    /// "evolution-api" vazam a palavra "api" e empatavam com o item
    /// "api latência" de verdade.
    /// Pedido do usuário ("gráfico dinâmico por tipo de acontecimento"):
    /// quando o título indica a métrica (temperatura, CPU, memória,
    /// tráfego, disco), itens do MESMO tipo têm prioridade — alerta de
    /// temperatura mostra a série de temperatura, não a de CPU.
    /// </summary>
    private static ZabbixItemDto? ResolveItem(
        IReadOnlyList<ZabbixItemDto> items, string alertTitle, HashSet<string> alertWords)
    {
        var metricKind = DetectMetricKind(alertTitle);
        var candidates = items
            .Select(item =>
            {
                var nameScore = alertWords.Count(Words(item.Name).Contains);
                var score = alertWords.Count(
                    Words($"{item.Name} {item.Key}").Contains);
                var kindMatch = metricKind is not MetricKind.Other
                    && MatchesMetricKind(item, metricKind);
                return (Item: item, NameScore: nameScore, Score: score, KindMatch: kindMatch);
            })
            .Where(candidate => candidate.Score > 0 || candidate.KindMatch)
            .OrderByDescending(candidate => candidate.KindMatch)
            .ThenByDescending(candidate => candidate.NameScore)
            .ThenByDescending(candidate => candidate.Score)
            .ToList();

        if (candidates.Count > 0
            && (candidates.Count == 1
                || candidates[0].KindMatch != candidates[1].KindMatch
                || candidates[0].NameScore != candidates[1].NameScore
                || candidates[0].Score != candidates[1].Score))
        {
            return candidates[0].Item;
        }

        // Empate entre JANELAS do mesmo item ("Load average 1m/5m/15m",
        // key system.cpu.load[all,avgN]): não é ambiguidade de métrica —
        // a key sem parâmetros é idêntica. Escolha determinística pela
        // key ordinal (avg1 < avg15 < avg5) e o MetricLabel mostra qual.
        // Métricas realmente diferentes (cpu.util vs cpu.util.other,
        // net.if.in vs net.if.out) seguem ambíguas → sem série.
        if (candidates.Count > 1)
        {
            var top = candidates[0];
            var tied = candidates
                .Where(candidate => candidate.KindMatch == top.KindMatch
                    && candidate.NameScore == top.NameScore
                    && candidate.Score == top.Score)
                .ToList();
            if (tied.Count > 1
                && tied.All(candidate =>
                    string.Equals(BaseKey(candidate.Item.Key),
                        BaseKey(tied[0].Item.Key), StringComparison.Ordinal)))
            {
                return tied
                    .OrderBy(candidate => candidate.Item.Key,
                        KeyWindowComparer.Instance)
                    .First().Item;
            }
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

    /// <summary>Key Zabbix sem os parâmetros entre colchetes.</summary>
    private static string BaseKey(string key)
    {
        var bracket = key.IndexOf('[');
        return bracket < 0 ? key : key[..bracket];
    }

    /// <summary>
    /// Comparador de key para desempate de janelas: base ordinal e cada
    /// parâmetro entre colchetes por segmento — numérico quando ambos são
    /// dígitos, ordinal caso contrário. Garante "avg1" &lt; "avg5" &lt;
    /// "avg15" (ordinal puro inverteria avg15 antes de avg1 por causa
    /// do ']' fechar antes do dígito extra).
    /// </summary>
    private sealed class KeyWindowComparer : IComparer<string>
    {
        public static readonly KeyWindowComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            var baseCompare = string.CompareOrdinal(BaseKey(x ?? ""), BaseKey(y ?? ""));
            if (baseCompare != 0)
                return baseCompare;

            var xParams = Params(x ?? "");
            var yParams = Params(y ?? "");
            for (var index = 0; index < Math.Min(xParams.Length, yParams.Length); index++)
            {
                var paramCompare = CompareParam(xParams[index], yParams[index]);
                if (paramCompare != 0)
                    return paramCompare;
            }
            return xParams.Length.CompareTo(yParams.Length);
        }

        private static string[] Params(string key)
        {
            var open = key.IndexOf('[');
            var close = key.IndexOf(']', open < 0 ? 0 : open);
            if (open < 0 || close <= open)
                return [];
            return key[(open + 1)..close].Split(',');
        }

        private static int CompareParam(string x, string y)
        {
            if (long.TryParse(x, out var xNumber) && long.TryParse(y, out var yNumber))
                return xNumber.CompareTo(yNumber);
            // "avg1" vs "avg15": compara o sufixo numérico quando o prefixo
            // é igual — senão "avg15" ganharia por ser prefixo mais longo? Não:
            // ordinal já resolve "avg1" &lt; "avg15" (prefixo menor primeiro).
            return string.CompareOrdinal(x, y);
        }
    }

    private enum MetricKind
    { Other, Temperature, Cpu, Memory, Traffic, Disk, Snmp, Availability }

    /// <summary>
    /// Tipo de métrica indicado pelo título do alerta (sem diacríticos,
    /// minúsculo). Ordem importa: "temperatura da CPU" casa CPU antes de
    /// temperatura só se CPU vier primeiro — aqui temperatura vem primeiro
    /// porque "temp cpu" raramente existe; o caso comum é um por alerta.
    /// SNMP vem antes de Availability: "SNMP indisponível" deve achar o
    /// item snmp.*, não o icmpping genérico.
    /// </summary>
    private static MetricKind DetectMetricKind(string alertTitle)
    {
        var plain = Normalize(alertTitle);
        // "temp"/"cpu"/"ram" são curtos demais para contains puro:
        // "tempo de resposta" e "documento" não são temperatura/memória.
        if (ContainsAny(plain, "temperatura", "temperature", "celsius",
                "thermal", "°c")
            || ContainsWord(plain, "temp"))
            return MetricKind.Temperature;
        if (ContainsAny(plain, "processador", "processer", "processamento")
            || ContainsWord(plain, "cpu"))
            return MetricKind.Cpu;
        if (ContainsAny(plain, "memoria", "memory", "swap")
            || ContainsWord(plain, "ram"))
            return MetricKind.Memory;
        if (ContainsAny(plain, "trafego", "traffic", "banda", "throughput",
                "interface", "rede", "network", "uplink", "downlink"))
            return MetricKind.Traffic;
        if (ContainsAny(plain, "disco", "disk", "storage", "espaco"))
            return MetricKind.Disk;
        if (ContainsAny(plain, "snmp"))
            return MetricKind.Snmp;
        if (ContainsAny(plain, "latencia", "latency", "perda", "loss",
                "ping", "indisponivel", "disponibilidade", "fora do ar",
                "unreachable"))
            return MetricKind.Availability;
        return MetricKind.Other;
    }

    /// <summary>
    /// O item é do mesmo tipo de métrica? Casa palavras-chave no nome E na
    /// key — keys Zabbix trazem o tipo ("temp", "cpu", "net.if.in").
    /// </summary>
    private static bool MatchesMetricKind(ZabbixItemDto item, MetricKind kind)
    {
        var haystack = Normalize($"{item.Name} {item.Key}");
        return kind switch
        {
            MetricKind.Temperature => ContainsAny(haystack,
                "temperatura", "celsius", "thermal", "°c")
                || ContainsWord(haystack, "temp"),
            MetricKind.Cpu => ContainsAny(haystack,
                "processador", "processer") || ContainsWord(haystack, "cpu"),
            MetricKind.Memory => ContainsAny(haystack,
                "memoria", "memory", "swap") || ContainsWord(haystack, "ram"),
            MetricKind.Traffic => ContainsAny(haystack,
                "trafego", "traffic", "banda", "throughput", "interface",
                "net.if", "network", "uplink", "downlink"),
            MetricKind.Disk => ContainsAny(haystack,
                "disco", "disk", "storage", "espaco"),
            MetricKind.Snmp => ContainsAny(haystack, "snmp"),
            MetricKind.Availability => ContainsAny(haystack,
                "icmp", "ping", "loss", "perda", "latency", "latencia",
                "net.tcp", "availability", "uptime"),
            _ => false,
        };
    }

    private static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        return new string(normalized
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character)
                != UnicodeCategory.NonSpacingMark)
            .ToArray()).ToLowerInvariant();
    }

    private static bool ContainsAny(string haystack, params string[] needles)
        => needles.Any(needle => haystack.Contains(needle, StringComparison.Ordinal));

    /// <summary>
    /// Casa o token inteiro — "temp" não pega "tempo", mas pega
    /// "system.cpu.util" (split por não-alfanumérico: cpu é token).
    /// </summary>
    private static bool ContainsWord(string haystack, string word)
        => new string(haystack
                .Select(c => char.IsLetterOrDigit(c) ? c : ' ')
                .ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(token => token == word);

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
    /// Host Zabbix exato via aggregate da notification: Alert →
    /// MonitoringEvent.SourceContextJson → HostIds[0] casado no
    /// host_id do read-model. Cobre alertas sem device mapeado e evita
    /// o fuzzy por título (que confunde prefixo de template com host).
    /// </summary>
    private async Task<ZabbixHostDto?> ResolveHostByAggregateAsync(
        Guid? aggregateId, CancellationToken cancellationToken)
    {
        if (aggregateId is not { } id || id == Guid.Empty)
            return null;

        try
        {
            var alert = await alertRepository.GetByIdAsync(id, cancellationToken);
            if (alert?.MonitoringEventId is not { } eventId || eventId == Guid.Empty)
                return null;

            var monitoringEvent = await monitoringEventRepository
                .GetByIdAsync(eventId, cancellationToken);
            var hostIds = ParseHostIds(monitoringEvent?.SourceContextJson);
            if (hostIds.Count == 0)
                return null;

            var hosts = await zabbixClient.ListHostsAsync(cancellationToken);
            return hosts.FirstOrDefault(host => host.Enabled
                && hostIds.Contains(host.HostId));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogDebug(exception,
                "Gráfico WhatsApp: falha ao resolver host pelo aggregate {AggregateId}.",
                aggregateId);
            return null;
        }
    }

    /// <summary>Extrai "HostIds" do SourceContextJson do MonitoringEvent.</summary>
    public static IReadOnlyList<string> ParseHostIds(string? sourceContextJson)
    {
        if (string.IsNullOrWhiteSpace(sourceContextJson))
            return [];

        try
        {
            using var document = JsonDocument.Parse(sourceContextJson);
            if (!document.RootElement.TryGetProperty("HostIds", out var ids)
                || ids.ValueKind != JsonValueKind.Array)
                return [];

            return ids.EnumerateArray()
                .Where(element => element.ValueKind == JsonValueKind.String)
                .Select(element => element.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Resolve o host Zabbix direto do alerta quando não há Device nem
    /// aggregate: o nome do problema vem como "{HOST}: {trigger}" — o
    /// trecho antes do primeiro ": " é o nome do host. Match exato por
    /// name/host; senão um único contains (ambíguo = null).
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

    /// <summary>
    /// ADR-069: toggle por tipo de cartão. Sem linha persistida (config só
    /// de env) vale o default — todos ligados.
    /// </summary>
    private static bool IsCardEnabled(
        WhatsAppIntegrationSetting? settings, ChartCardKind kind)
        => settings is null || kind switch
        {
            ChartCardKind.Incident => settings.CardIncidentEnabled,
            ChartCardKind.Warning => settings.CardWarningEnabled,
            _ => settings.CardResolutionEnabled,
        };

    private static string? AccentColorFor(
        WhatsAppIntegrationSetting? settings, ChartCardKind kind)
        => kind switch
        {
            ChartCardKind.Incident => settings?.CardIncidentColor,
            ChartCardKind.Warning => settings?.CardWarningColor,
            _ => settings?.CardResolutionColor,
        };

    /// <summary>
    /// ADR-069: triggerId do estado (CorrelationKey "Zabbix:trigger:{id}")
    /// → trigger.get → threshold da expressão. No RESOLVIDO tenta antes a
    /// recovery_expression (limite de normalização); fallback = expressão
    /// do problema. Qualquer falha → null (cartão sem linha de limite).
    /// </summary>
    private async Task<double?> ResolveLimitValueAsync(
        MonitoringProblemState? state, ChartCardKind kind,
        CancellationToken cancellationToken)
    {
        var triggerId = TryGetTriggerId(state?.CorrelationKey);
        if (triggerId is null || !zabbixClient.IsConfigured)
            return null;

        try
        {
            var trigger = await zabbixClient.GetTriggerAsync(
                triggerId, cancellationToken);
            if (trigger is null)
                return null;

            return kind == ChartCardKind.Resolution
                ? ZabbixTriggerThreshold.TryParseRecovery(trigger.RecoveryExpression)
                    ?? ZabbixTriggerThreshold.TryParseProblem(trigger.Expression)
                : ZabbixTriggerThreshold.TryParseProblem(trigger.Expression);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception,
                "Gráfico WhatsApp: falha ao resolver threshold da trigger {TriggerId}.", triggerId);
            return null;
        }
    }

    /// <summary>
    /// ADR-069: duração do ciclo — RESOLVIDO = OpenedAt→ResolvedAt; demais
    /// tipos = OpenedAt→eventAtUtc ("aberto há"). Null sem estado/ciclo.
    /// </summary>
    private static TimeSpan? ResolveCycleDuration(
        MonitoringProblemState? state, ChartCardKind kind, DateTime eventAtUtc)
    {
        if (state is null)
            return null;

        var end = kind == ChartCardKind.Resolution
            ? state.ResolvedAt ?? eventAtUtc
            : eventAtUtc;
        var duration = end - state.OpenedAt;
        return duration > TimeSpan.Zero ? duration : null;
    }

    /// <summary>Extrai o triggerId de "Zabbix:trigger:{id}"; null em fallback name:/outros.</summary>
    internal static string? TryGetTriggerId(string? correlationKey)
    {
        const string prefix = "Zabbix:trigger:";
        return correlationKey is not null
            && correlationKey.StartsWith(prefix, StringComparison.Ordinal)
            && correlationKey.Length > prefix.Length
                ? correlationKey[prefix.Length..]
                : null;
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

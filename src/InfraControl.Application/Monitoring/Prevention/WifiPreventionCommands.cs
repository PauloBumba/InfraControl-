using System.Globalization;
using InfraControl.Application.Alerts;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfraControl.Application.Monitoring.Prevention;

// F30-T004 S4 (ADR-056 aceito 2026-10-15): prevenção WiFi por tendência
// sobre o histórico do Zabbix. Opt-in = device com SnmpRadio no perfil
// (configuração explícita por device que já existe — sem migração nova).
// REGRA da casa: pré-requisito ausente (host/item/chave/histórico) =
// avaliação honesta com motivo em pt-BR — nunca "saudável" inventado; o
// Alert só é tocado quando ao menos UMA métrica foi lida.

/// <summary>
/// F30-T004 S4: chaves de item do Zabbix por métrica de prevenção. VAZIO =
/// regra não roda (motivo honesto no DTO) — cada template Zabbix tem suas
/// chaves e o operador preenche por ambiente (ADR-056 §2: métrica ausente
/// não vira 0). Bind da seção "WifiPrevention" do appsettings.
/// </summary>
public sealed class WifiPreventionOptions
{
    public const string SectionName = "WifiPrevention";

    public string UtilizationItemKey { get; set; } = string.Empty;

    public string RssiItemKey { get; set; } = string.Empty;

    public string ClientsItemKey { get; set; } = string.Empty;
}

/// <summary>Executa a avaliação de prevenção WiFi de um device (ADR-056).</summary>
public sealed record EvaluateWifiPreventionCommand(Guid DeviceId)
    : BaseCommand<WifiPreventionEvaluationDto>, IAuthorizedRequest
{
    // ADR-030 §3: mutação — mesma camada/policy HTTP do RunCheck (Operate);
    // Viewer lê, não dispara avaliação que escreve Alert.
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

/// <summary>
/// Estado de uma regra na avaliação: <c>Status</c> = Fired / Clean /
/// Skipped. Skipped carrega <c>Reason</c> honesto (chave/item/amostras) —
/// nunca 0 (ADR-056 §2).
/// </summary>
public sealed record WifiPreventionRuleDto(
    string Rule,
    string Status,
    string? Reason,
    string? Severity,
    string? Message);

/// <summary>Resultado da avaliação de prevenção de UM device.</summary>
public sealed record WifiPreventionEvaluationDto(
    Guid DeviceId,
    string DeviceName,
    bool Evaluated,
    string? Reason,
    IReadOnlyList<WifiPreventionRuleDto> Rules,
    int AlertsOpened,
    int AlertsResolved);

public sealed class EvaluateWifiPreventionCommandHandler(
    IDeviceRepository deviceRepository,
    IZabbixHostsRepository hostsRepository,
    IZabbixClient zabbixClient,
    IAlertEvaluator alertEvaluator,
    IOptions<WifiPreventionOptions> optionsAccessor,
    ILogger<EvaluateWifiPreventionCommandHandler> logger)
    : IRequestHandler<EvaluateWifiPreventionCommand, WifiPreventionEvaluationDto>
{
    private const int HistoryLimit = 500;
    private const string StatusFired = "Fired";
    private const string StatusClean = "Clean";
    private const string StatusSkipped = "Skipped";

    private static readonly IReadOnlyList<string> UtilizationRules =
    [
        WifiPreventionEvaluator.RadioSaturationCriticalRule,
        WifiPreventionEvaluator.RadioSaturationRule,
        WifiPreventionEvaluator.RadioTrendRule,
    ];

    private static readonly IReadOnlyList<string> RssiRules =
    [
        WifiPreventionEvaluator.RssiDegradationRule,
    ];

    private static readonly IReadOnlyList<string> ClientRules =
    [
        WifiPreventionEvaluator.ClientDensityRule,
    ];

    public async Task<WifiPreventionEvaluationDto> Handle(
        EvaluateWifiPreventionCommand request,
        CancellationToken cancellationToken)
    {
        var device = await deviceRepository
            .GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Device '{request.DeviceId}' não encontrado.");

        if (device.Type != DeviceType.AccessPoint)
        {
            throw new ArgumentException(
                "A prevenção WiFi só se aplica a devices do tipo AccessPoint.",
                nameof(request.DeviceId));
        }

        if (!device.MonitoringChecks.Contains(CheckKind.SnmpRadio))
        {
            return Blocked(device,
                "Prevenção não habilitada no device — ative o check "
                + "SnmpRadio no perfil do AP.");
        }

        if (string.IsNullOrWhiteSpace(device.Ip))
        {
            return Blocked(device,
                "Device sem IP cadastrado — sem como mapear o host Zabbix.");
        }

        if (!zabbixClient.IsConfigured)
        {
            return Blocked(device, "Integração Zabbix não configurada.");
        }

        var host = (await hostsRepository.ListAsync(cancellationToken))
            .FirstOrDefault(entry => entry.Enabled
                && entry.MissingSinceUtc is null
                && !string.IsNullOrWhiteSpace(entry.Ip)
                && string.Equals(
                    entry.Ip, device.Ip, StringComparison.OrdinalIgnoreCase));
        if (host is null)
        {
            return Blocked(device,
                $"Device sem host Zabbix mapeado para o IP {device.Ip} "
                + "(sync F27).");
        }

        var options = optionsAccessor.Value;
        var toUtc = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        var fromUtc = toUtc.AddMinutes(-WifiPreventionThresholds.TrendWindowMinutes);
        var items = await zabbixClient.ListItemsAsync(host.HostId, cancellationToken);

        var utilization = await ReadMetricAsync(
            items, options.UtilizationItemKey, "UtilizationItemKey",
            host.HostId, fromUtc, toUtc, cancellationToken);
        var rssi = await ReadMetricAsync(
            items, options.RssiItemKey, "RssiItemKey",
            host.HostId, fromUtc, toUtc, cancellationToken);
        var clients = await ReadMetricAsync(
            items, options.ClientsItemKey, "ClientsItemKey",
            host.HostId, fromUtc, toUtc, cancellationToken);

        if (utilization.Points is null
            && rssi.Points is null
            && clients.Points is null)
        {
            var reason = utilization.SkipReason
                ?? rssi.SkipReason
                ?? clients.SkipReason
                ?? "Nenhuma métrica de prevenção legível.";
            return new WifiPreventionEvaluationDto(
                device.Id, device.Name, Evaluated: false, reason,
                SkippedRules(utilization, rssi, clients),
                AlertsOpened: 0, AlertsResolved: 0);
        }

        var evaluations = WifiPreventionEvaluator.Evaluate(
            device.Name, utilization.Points, rssi.Points, clients.Points);

        var rules = new List<WifiPreventionRuleDto>(evaluations.Count);
        AddRules(rules, evaluations, UtilizationRules, utilization.SkipReason);
        AddRules(rules, evaluations, RssiRules, rssi.SkipReason);
        AddRules(rules, evaluations, ClientRules, clients.SkipReason);

        var fired = evaluations
            .Where(entry => entry.Verdict is not null)
            .Select(entry => entry.Verdict!)
            .ToList();

        var alertsOpened = 0;
        var alertsResolved = 0;

        if (fired.Count > 0)
        {
            // Pior veredito primeiro (Critical > Warning — ADR-056 §3).
            var worst = fired.MaxBy(verdict => verdict.Severity)!;
            if (await alertEvaluator.EvaluateVerdictAsync(
                    device, CheckKind.SnmpRadio, worst.Severity,
                    worst.Message, cancellationToken))
            {
                alertsOpened = 1;
            }

            logger.LogInformation(
                "Prevenção WiFi em {DeviceId} ({Name}): {Rule} = "
                + "{Severity}. {Message}",
                device.Id, device.Name, worst.Rule, worst.Severity,
                worst.Message);
        }
        else if (await alertEvaluator.ResolveVerdictAsync(
                     device, CheckKind.SnmpRadio, cancellationToken))
        {
            // Sem regra ativa entre as métricas LIDAS = normalizado
            // (métrica não lida não decide — regra não roda, ADR-056 §2).
            alertsResolved = 1;
        }

        return new WifiPreventionEvaluationDto(
            device.Id, device.Name, Evaluated: true, Reason: null,
            rules, alertsOpened, alertsResolved);
    }

    /// <summary>
    /// Lê o histórico da janela para UMA métrica: chave vazia/item ausente
    /// = skip com motivo específico (combinado no DTO); valores não
    /// numéricos são descartados — a contagem honesta fica por conta do
    /// avaliador (N ≥ <see cref="WifiPreventionThresholds.MinSamples"/>).
    /// </summary>
    private async Task<MetricReading> ReadMetricAsync(
        IReadOnlyList<ZabbixItemDto> items,
        string key,
        string optionName,
        string hostId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return new MetricReading(null,
                $"chave de item não configurada (WifiPrevention:{optionName})");
        }

        var item = items.FirstOrDefault(entry =>
            string.Equals(entry.Key, key, StringComparison.Ordinal)
            && entry.Enabled
            && !entry.Unsupported);
        if (item is null)
        {
            return new MetricReading(null,
                $"item '{key}' ausente no host {hostId}");
        }

        var history = await zabbixClient.GetHistoryAsync(
            item.ItemId, fromUtc, toUtc, HistoryLimit, cancellationToken);

        var points = new List<WifiSeriesPoint>(history.Count);
        foreach (var value in history)
        {
            if (double.TryParse(value.Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var parsed))
            {
                points.Add(new WifiSeriesPoint(value.ClockUtc, parsed));
            }
        }

        return new MetricReading(points, null);
    }

    /// <summary>
    /// Mescla vereditos do avaliador com skips por métrica: métrica não
    /// lida (chave/item) carrega o motivo ESPECÍFICO do comando; métrica
    /// lida passa pelo avaliador (que tem os motivos de amostra).
    /// </summary>
    private static void AddRules(
        List<WifiPreventionRuleDto> target,
        IReadOnlyList<WifiRuleEvaluation> evaluations,
        IReadOnlyList<string> ruleIds,
        string? metricSkipReason)
    {
        foreach (var ruleId in ruleIds)
        {
            if (metricSkipReason is not null)
            {
                target.Add(new WifiPreventionRuleDto(
                    ruleId, StatusSkipped, metricSkipReason, null, null));
                continue;
            }

            var evaluation = evaluations.Single(entry => entry.Rule == ruleId);
            target.Add(evaluation.Verdict is not null
                ? new WifiPreventionRuleDto(
                    ruleId, StatusFired, null,
                    evaluation.Verdict.Severity.ToString(),
                    evaluation.Verdict.Message)
                : evaluation.Evaluated
                    ? new WifiPreventionRuleDto(
                        ruleId, StatusClean, null, null, null)
                    : new WifiPreventionRuleDto(
                        ruleId, StatusSkipped, evaluation.SkipReason, null, null));
        }
    }

    private static IReadOnlyList<WifiPreventionRuleDto> SkippedRules(
        MetricReading utilization, MetricReading rssi, MetricReading clients)
    {
        var rules = new List<WifiPreventionRuleDto>(5);
        AddRules(rules, [], UtilizationRules, utilization.SkipReason);
        AddRules(rules, [], RssiRules, rssi.SkipReason);
        AddRules(rules, [], ClientRules, clients.SkipReason);
        return rules;
    }

    private static WifiPreventionEvaluationDto Blocked(Device device, string reason)
        => new(
            device.Id, device.Name, Evaluated: false, reason,
            [
                new(WifiPreventionEvaluator.RadioSaturationCriticalRule,
                    StatusSkipped, reason, null, null),
                new(WifiPreventionEvaluator.RadioSaturationRule,
                    StatusSkipped, reason, null, null),
                new(WifiPreventionEvaluator.RadioTrendRule,
                    StatusSkipped, reason, null, null),
                new(WifiPreventionEvaluator.RssiDegradationRule,
                    StatusSkipped, reason, null, null),
                new(WifiPreventionEvaluator.ClientDensityRule,
                    StatusSkipped, reason, null, null),
            ],
            AlertsOpened: 0, AlertsResolved: 0);

    private sealed record MetricReading(
        IReadOnlyList<WifiSeriesPoint>? Points,
        string? SkipReason);
}

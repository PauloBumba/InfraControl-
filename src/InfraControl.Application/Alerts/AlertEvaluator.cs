using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Alerts;

// F6-T001/F6-T002: thresholds e regras FIXOS em código (decisão do
// usuário — generalizar para perfil/DeviceProfile fica para task+ADR
// futura). Avaliação pós-check; anti-storm = 1 Open por (device, check) +
// streak de falhas consecutivas no histórico (N=3 para Ping).

public static class AlertThresholds
{
    public const int TonerMinPercent = 20;
    public const int DrumMinPercent = 10;

    /// <summary>F30-T002a: pior toner colorido (C/M/Y) abaixo = Warning.</summary>
    public const int ColorTonerMinPercent = 20;

    /// <summary>Falhas consecutivas de Ping antes de abrir o alerta (F6-T002).</summary>
    public const int PingFailureStreak = 3;
}

/// <summary>
/// Resultado da avaliação de um evento externo (F19-T003). <see cref="Suppressed"/>
/// = nasceu ligado a um incidente upstream (correlação BFS); <see cref="IncidentId"/>
/// nulo = severidade não-Critical (Warning não abre incidente).
/// </summary>
public sealed record ExternalEvaluationResult(
    Guid AlertId, Guid? IncidentId, bool Suppressed);

/// <summary>
/// Avalia o resultado de um check e cria/resolve o Alert correspondente
/// (pós-check, mesmo SaveChanges do MonitoringResult).
/// </summary>
public interface IAlertEvaluator
{
    Task EvaluateAsync(
        Device device,
        CheckKind kind,
        MonitoringResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// F19-T003 — evento externo (Zabbix) sem re-aplicar streaks: o Zabbix é
    /// a autoridade da falha. Pipeline: identidade/idempotência → Device →
    /// correlação BFS → supressão → Alert → Incident SOMENTE se Critical
    /// (regras 1 e 4 da revisão arquitetural das F19/F20/F21).
    /// </summary>
    Task<ExternalEvaluationResult> EvaluateExternalAsync(
        MonitoringEvent monitoringEvent,
        string? host,
        string? ip,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// F30-T004 S4 (ADR-056): aplica um veredito de PREVENÇÃO calculado
    /// fora do pipeline de check (janela do Zabbix) ao MESMO ciclo de vida
    /// — Open/Suppressed por (device, kind), supressão por incidente
    /// upstream (F6-T004), Critical auto-abre Incident (F6-T003). Retorna
    /// true quando um alerta novo foi aberto.
    /// </summary>
    Task<bool> EvaluateVerdictAsync(
        Device device,
        CheckKind kind,
        AlertSeverity severity,
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// F30-T004 S4: normalização da prevenção — resolve o alerta ativo de
    /// (device, kind) se houver (recovery fecha incidentes — F6-T006).
    /// Retorna true quando um alerta foi resolvido.
    /// </summary>
    Task<bool> ResolveVerdictAsync(
        Device device,
        CheckKind kind,
        CancellationToken cancellationToken = default);
}

public sealed class AlertEvaluator(
    IAlertRepository repository,
    IMonitoringResultRepository resultRepository,
    IIncidentRepository incidentRepository,
    IUpstreamCorrelationRepository correlationRepository,
    IRootCauseResolver rootCauseResolver) : IAlertEvaluator
{
    public async Task EvaluateAsync(
        Device device,
        CheckKind kind,
        MonitoringResult result,
        CancellationToken cancellationToken = default)
    {
        switch (kind)
        {
            case CheckKind.Ping:
                if (result.Succeeded)
                {
                    await ResolveActiveAsync(device.Id, kind, cancellationToken);
                }
                else
                {
                    // F6-T002: só abre com N falhas consecutivas — N-1 do
                    // histórico (banco) + o atual. Sucesso salvo no meio
                    // (próximo ciclo) quebra a sequência.
                    var required = AlertThresholds.PingFailureStreak - 1;
                    var previous = await resultRepository
                        .GetRecentByDeviceAndKindAsync(
                            device.Id, kind, required, cancellationToken);

                    if (previous.Count >= required
                        && previous.All(entry => !entry.Succeeded))
                    {
                        await OpenOrSuppressAsync(
                            device, kind, AlertSeverity.Critical,
                            $"Device '{device.Name}' fora do ar "
                                + $"({AlertThresholds.PingFailureStreak} falhas "
                                + "consecutivas no ping).",
                            cancellationToken);
                    }
                }
                break;

            case CheckKind.SnmpToner:
                await EvaluateSupplyAsync(
                    device, kind, result, AlertThresholds.TonerMinPercent, "Toner",
                    cancellationToken);
                break;

            case CheckKind.SnmpDrum:
                await EvaluateSupplyAsync(
                    device, kind, result, AlertThresholds.DrumMinPercent, "Drum",
                    cancellationToken);
                break;

            case CheckKind.SnmpColorToner:
                await EvaluateColorTonerAsync(device, kind, result, cancellationToken);
                break;

            case CheckKind.SnmpPrinterStatus:
                await EvaluatePrinterStatusAsync(device, kind, result, cancellationToken);
                break;

            default:
                // F6-T001 cobre só Ping/toner/drum; demais kinds são F6-T002+.
                break;
        }

        // F6-T005: reavalia a causa provável do Incident Open do device
        // (cobre a corrida: upstream/vizinho abriram depois; incidente
        // recém-criado ganhou causa direto no EnsureIncidentAsync).
        await rootCauseResolver.RefreshAsync(device.Id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ExternalEvaluationResult> EvaluateExternalAsync(
        MonitoringEvent monitoringEvent,
        string? host,
        string? ip,
        CancellationToken cancellationToken = default)
    {
        // 1. Identidade/idempotência (regra 4): Alert já existe para
        // (Source, ExternalProblemId) = replay do mesmo problem — devolve o
        // estado atual sem criar Alert nem Incident novos.
        var existing = await repository.GetByExternalAsync(
            monitoringEvent.Source,
            monitoringEvent.ExternalProblemId,
            cancellationToken);
        if (existing is not null)
        {
            var linked = await incidentRepository
                .GetUnresolvedIdsLinkedToAlertAsync(existing.Id, cancellationToken);
            return new(
                existing.Id,
                linked.Count > 0 ? linked[0] : null,
                existing.Status == AlertStatus.Suppressed);
        }

        // 2. Alert — SEM reaplicar streak (regra 1): a severidade vem da
        // fonte, o Zabbix é a autoridade da falha.
        var alert = Alert.CreateExternal(
            monitoringEvent.Id,
            monitoringEvent.ExternalProblemId,
            monitoringEvent.DeviceId,
            kind: null,
            MapAlertSeverity(monitoringEvent.Severity),
            monitoringEvent.Message,
            monitoringEvent.Source);

        // 3. Correlação/topologia (BFS) → supressão: device alcançável com
        // incidente aberto = vítima; o alerta nasce Suppressed e entra no
        // incidente upstream — sem incidente próprio.
        if (monitoringEvent.DeviceId is { } deviceId)
        {
            var upstream = await correlationRepository
                .FindOpenIncidentUpstreamAsync(deviceId, cancellationToken);
            if (upstream is not null)
            {
                alert.Suppress();
                await repository.AddAsync(alert, cancellationToken);
                await incidentRepository.AddLinkAsync(
                    IncidentAlert.Create(upstream.IncidentId, alert.Id),
                    cancellationToken);
                return new(alert.Id, upstream.IncidentId, Suppressed: true);
            }
        }

        await repository.AddAsync(alert, cancellationToken);

        // 4. Incident SOMENTE se Critical (regra 1; Warning fica só Alert).
        if (alert.Severity != AlertSeverity.Critical)
            return new(alert.Id, null, Suppressed: false);

        // 1 incidente por alert/problema (ADR-027 §3 — não reutiliza o
        // GetOpenByDeviceAsync do nativo).
        var incident = Incident.Create(
            alert.Message,
            MapIncidentSeverity(monitoringEvent.Severity),
            monitoringEvent.DeviceId,
            BuildExternalDescription(monitoringEvent, host, ip));
        await incidentRepository.AddAsync(incident, cancellationToken);
        await incidentRepository.AddLinkAsync(
            IncidentAlert.Create(incident.Id, alert.Id), cancellationToken);

        return new(alert.Id, incident.Id, Suppressed: false);
    }

    /// <summary>Severidade Zabbix (6 níveis) → IncidentSeverity (4 da UI).</summary>
    internal static IncidentSeverity MapIncidentSeverity(int severity) => severity switch
    {
        0 or 1 => IncidentSeverity.Low,
        2 => IncidentSeverity.Medium,
        3 => IncidentSeverity.High,
        _ => IncidentSeverity.Critical
    };

    /// <summary>Severidade Zabbix → AlertSeverity (0..2 Warning, 3..5 Critical).</summary>
    internal static AlertSeverity MapAlertSeverity(int severity)
        => severity >= 3 ? AlertSeverity.Critical : AlertSeverity.Warning;

    /// <summary>Origem do evento para auditoria humana (partes presentes).</summary>
    private static string BuildExternalDescription(
        MonitoringEvent monitoringEvent, string? host, string? ip)
    {
        var parts = new List<string>(3)
        {
            $"evento {monitoringEvent.ExternalEventId}"
        };
        if (!string.IsNullOrWhiteSpace(host))
            parts.Add($"host {host.Trim()}");
        if (!string.IsNullOrWhiteSpace(ip))
            parts.Add($"ip {ip.Trim()}");

        return $"Recebido de {monitoringEvent.Source} — {string.Join(", ", parts)}.";
    }

    private async Task EvaluateSupplyAsync(
        Device device,
        CheckKind kind,
        MonitoringResult result,
        int minPercent,
        string label,
        CancellationToken cancellationToken)
    {
        // Erro de SNMP (Succeeded=false, Values=null) ou parse falho →
        // nível desconhecido: não avalia (não é "baixo").
        if (result.Values is null
            || !result.Values.TryGetValue("levelPercent", out var raw)
            || !int.TryParse(raw, out var percent))
            return;

        if (percent < minPercent)
        {
            // Warning não toca incidente (só Critical auto-abre — F6-T003).
            await OpenOrSuppressAsync(
                device, kind, AlertSeverity.Warning,
                $"{label} de '{device.Name}' em {percent}% (mínimo {minPercent}%).",
                cancellationToken);
        }
        else
        {
            await ResolveActiveAsync(device.Id, kind, cancellationToken);
        }
    }

    /// <summary>
    /// F30-T002a: avalia a pior cor (worstPercent/worstColor do provider)
    /// contra o limiar de toner colorido. Sem worstPercent = erro de SNMP
    /// ou impressora mono-only → não avalia.
    /// </summary>
    private async Task EvaluateColorTonerAsync(
        Device device,
        CheckKind kind,
        MonitoringResult result,
        CancellationToken cancellationToken)
    {
        if (result.Values is null
            || !result.Values.TryGetValue("worstPercent", out var raw)
            || !int.TryParse(raw, out var percent))
            return;

        result.Values.TryGetValue("worstColor", out var color);
        var colorLabel = string.IsNullOrWhiteSpace(color) ? "Toner colorido" : $"Toner {color}";

        if (percent < AlertThresholds.ColorTonerMinPercent)
        {
            await OpenOrSuppressAsync(
                device, kind, AlertSeverity.Warning,
                $"{colorLabel} de '{device.Name}' em {percent}% "
                    + $"(mínimo {AlertThresholds.ColorTonerMinPercent}%).",
                cancellationToken);
        }
        else
        {
            await ResolveActiveAsync(device.Id, kind, cancellationToken);
        }
    }

    /// <summary>
    /// F30-T002b: estado da impressora — pior condição prevalece (ordem:
    /// atolamento Critical > todas as bandejas vazias Critical > tampa
    /// Warning > bandeja parcial Warning > alertas ativos Warning). Sem
    /// condição → resolve. Values ausentes (erro SNMP) → não avalia.
    /// </summary>
    private async Task EvaluatePrinterStatusAsync(
        Device device,
        CheckKind kind,
        MonitoringResult result,
        CancellationToken cancellationToken)
    {
        if (result.Values is null)
            return;

        var inputCount = ReadInt(result.Values, "inputCount");
        var inputEmptyCount = ReadInt(result.Values, "inputEmptyCount");
        var inputJammedCount = ReadInt(result.Values, "inputJammedCount");
        var activeAlertCount = ReadInt(result.Values, "activeAlertCount");
        var coverOpen = result.Values.TryGetValue("coverOpen", out var rawCover)
            && string.Equals(rawCover, "true", StringComparison.OrdinalIgnoreCase);

        string? message = null;
        var severity = AlertSeverity.Warning;

        if (inputJammedCount is > 0)
        {
            severity = AlertSeverity.Critical;
            message = $"Atolamento de papel detectado em '{device.Name}' "
                + $"({inputJammedCount} bandeja(s) travada(s)).";
        }
        else if (inputCount is > 0 && inputEmptyCount >= inputCount)
        {
            severity = AlertSeverity.Critical;
            message = $"Todas as bandejas de '{device.Name}' estão vazias "
                + $"({inputEmptyCount}/{inputCount}).";
        }
        else if (coverOpen)
        {
            message = $"Tampa de '{device.Name}' aberta.";
        }
        else if (inputEmptyCount is > 0)
        {
            message = $"{inputEmptyCount} bandeja(s) de '{device.Name}' sem mídia.";
        }
        else if (activeAlertCount is > 0)
        {
            message = $"{activeAlertCount} alerta(s) ativo(s) na impressora '{device.Name}'.";
        }

        if (message is null)
        {
            await ResolveActiveAsync(device.Id, kind, cancellationToken);
            return;
        }

        await OpenOrSuppressAsync(device, kind, severity, message, cancellationToken);
    }

    private static int ReadInt(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed)
            ? parsed
            : 0;

    /// <summary>
    /// F30-T004 S4 (ADR-056): delega o veredito de prevenção calculado fora
    /// ao MESMO ciclo de vida dos alerts de check (Open/Suppressed com
    /// upstream, Critical auto-abre Incident — F6-T003/T004).
    /// </summary>
    public Task<bool> EvaluateVerdictAsync(
        Device device,
        CheckKind kind,
        AlertSeverity severity,
        string message,
        CancellationToken cancellationToken = default)
        => OpenOrSuppressAsync(device, kind, severity, message, cancellationToken);

    /// <summary>
    /// F30-T004 S4 (ADR-056): normalização da prevenção — resolve o alerta
    /// ativo de (device, kind) se houver (recovery fecha incidentes).
    /// </summary>
    public Task<bool> ResolveVerdictAsync(
        Device device,
        CheckKind kind,
        CancellationToken cancellationToken = default)
        => ResolveActiveAsync(device.Id, kind, cancellationToken);

    /// <summary>
    /// F6-T004: com upstream alcançável (BFS nas Connections) em Incident
    /// Open, o alerta de qualquer kind nasce/vira <see cref="AlertStatus.Suppressed"/>
    /// e é vinculado ao incidente raiz (N:N) — sem incidente próprio. Sem
    /// upstream: fluxo normal (Open; Critical auto-abre Incident — F6-T003;
    /// alerta Suppressed libera para Open).
    /// </summary>
    private async Task<bool> OpenOrSuppressAsync(
        Device device,
        CheckKind kind,
        AlertSeverity severity,
        string message,
        CancellationToken cancellationToken)
    {
        var upstream = await correlationRepository
            .FindOpenIncidentUpstreamAsync(device.Id, cancellationToken);
        var active = await repository
            .GetActiveAsync(device.Id, kind, cancellationToken);
        var created = active is null;

        if (upstream is not null)
        {
            var alert = active;
            if (alert is null)
            {
                alert = Alert.Create(device.Id, kind, severity, message);
                alert.Suppress();
                await repository.AddAsync(alert, cancellationToken);
            }
            else
            {
                alert.Suppress();
            }

            // Idempotente no repositório (já vinculado → skip).
            await incidentRepository.AddLinkAsync(
                IncidentAlert.Create(upstream.IncidentId, alert.Id),
                cancellationToken);
            return created;
        }

        if (active is null)
        {
            active = Alert.Create(device.Id, kind, severity, message);
            await repository.AddAsync(active, cancellationToken);
        }
        else
        {
            active.Reopen();
        }

        if (severity == AlertSeverity.Critical)
        {
            // F6-T003: Alert Critical auto-abre Incident.
            await EnsureIncidentAsync(device, active, cancellationToken);
        }

        return created;
    }

    private async Task<bool> ResolveActiveAsync(
        Guid deviceId, CheckKind kind, CancellationToken cancellationToken)
    {
        // Resolve Open e Suppressed (F6-T004) — condição normalizada.
        var active = await repository.GetActiveAsync(deviceId, kind, cancellationToken);
        if (active is null)
            return false;

        active.Resolve();

        // F6-T006: recovery — incidente fecha quando todos os alerts
        // vinculados normalizaram (sem vínculos = manual, só fecha por
        // comando).
        await CloseRecoveredIncidentsAsync(active, deviceId, cancellationToken);
        return true;
    }

    private async Task CloseRecoveredIncidentsAsync(
        Alert resolved, Guid deviceId, CancellationToken cancellationToken)
    {
        // ADR-027: não-Resolved (não só Open) — incidente em Investigating/
        // Identified/Monitoring também fecha quando a condição normaliza.
        var ids = new HashSet<Guid>(await incidentRepository
            .GetUnresolvedIdsLinkedToAlertAsync(resolved.Id, cancellationToken));

        var own = await incidentRepository
            .GetUnresolvedByDeviceAsync(deviceId, cancellationToken);
        if (own is not null)
            ids.Add(own.Id);

        foreach (var id in ids)
        {
            var incident = await incidentRepository
                .GetByIdAsync(id, cancellationToken);
            if (incident is null || incident.Status == IncidentStatus.Resolved)
                continue;

            var alerts = await incidentRepository
                .GetAlertsAsync(incident.Id, cancellationToken);
            if (alerts.Count == 0)
                continue;

            // GetAlertsAsync é AsNoTracking: o alert resolvido agora pode
            // vir com status antigo do banco — ele conta como resolvido.
            var allResolved = alerts.All(alert =>
                alert.Id == resolved.Id || alert.Status == AlertStatus.Resolved);

            if (allResolved)
                incident.ChangeStatus(IncidentStatus.Resolved);
        }
    }

    /// <summary>
    /// F6-T003: Alert Critical garante 1 Incident não-Resolved por device e
    /// vincula o alerta (N:N); alerta órfão (criado antes do T003) ganha
    /// vínculo na próxima avaliação. ADR-027: busca Status != Resolved —
    /// com Open-only, enquanto o incidente ficava em Investigating e o check
    /// continuava falhando, cada ciclo abria um novo incidente.
    /// </summary>
    private async Task EnsureIncidentAsync(
        Device device, Alert alert, CancellationToken cancellationToken)
    {
        var incident = await incidentRepository.GetUnresolvedByDeviceAsync(
            device.Id, cancellationToken);

        if (incident is null)
        {
            incident = Incident.Create(
                alert.Message, IncidentSeverity.Critical, deviceId: device.Id);
            incident.SetRootCause(
                await rootCauseResolver.ResolveAsync(incident, cancellationToken));
            await incidentRepository.AddAsync(incident, cancellationToken);
        }

        await incidentRepository.AddLinkAsync(
            IncidentAlert.Create(incident.Id, alert.Id), cancellationToken);
    }
}

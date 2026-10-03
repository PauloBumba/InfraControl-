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
    /// F6-T004: com upstream alcançável (BFS nas Connections) em Incident
    /// Open, o alerta de qualquer kind nasce/vira <see cref="AlertStatus.Suppressed"/>
    /// e é vinculado ao incidente raiz (N:N) — sem incidente próprio. Sem
    /// upstream: fluxo normal (Open; Critical auto-abre Incident — F6-T003;
    /// alerta Suppressed libera para Open).
    /// </summary>
    private async Task OpenOrSuppressAsync(
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
            return;
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
    }

    private async Task ResolveActiveAsync(
        Guid deviceId, CheckKind kind, CancellationToken cancellationToken)
    {
        // Resolve Open e Suppressed (F6-T004) — condição normalizada.
        var active = await repository.GetActiveAsync(deviceId, kind, cancellationToken);
        if (active is null)
            return;

        active.Resolve();

        // F6-T006: recovery — incidente fecha quando todos os alerts
        // vinculados normalizaram (sem vínculos = manual, só fecha por
        // comando).
        await CloseRecoveredIncidentsAsync(active, deviceId, cancellationToken);
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

using InfraControl.Application.Alerts;
using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Monitoring.Events.Ports;
using InfraControl.Domain.Common;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Monitoring.Events;

/// <summary>
/// Resultado da ingestão de um evento externo (token em pt/en do contrato
/// docs/27 — mapeado para created|duplicate|resolved|none pelo handler).
/// </summary>
public enum IngestOutcome
{
    Created,
    Duplicate,
    Resolved,
    None
}

/// <summary>
/// Entrada já validada pelo handler Zabbix (ADR-027): ids normalizados,
/// dispositivo resolvido e payload reconstruído.
/// </summary>
public sealed record ZabbixIngestInput(
    string EventId,
    string? RecoveryEventId,
    Guid? DeviceId,
    string? Host,
    string? Ip,
    string Name,
    int Severity,
    bool IsRecovery,
    DateTime? OccurredAt,
    string RawPayload);

/// <summary>Alert/Incident afetados (nulls quando não havia correspondência).</summary>
public sealed record MonitoringIngestResult(
    Guid? AlertId,
    Guid? IncidentId,
    IngestOutcome Outcome);

public interface IMonitoringEventIngestor
{
    Task<MonitoringIngestResult> IngestAsync(
        ZabbixIngestInput input, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pipeline PROBLEM→MonitoringEvent+Alert+Incident / OK→recovery (ADR-027):
/// dedup por (Source, ExternalEventId) — sem título, sem device como chave.
/// Um SaveChanges único por ingestão (evento + alerta + incidente + vínculo
/// são atômicos); corrida de duplicata simultânea estoura o índice único e o
/// retry do Zabbix cai no caminho duplicate (notificações do mesmo trigger são
/// sequenciais na fonte — doc do webhook). F19-T003: Alert/Incident vêm do
/// AlertEvaluator.EvaluateExternalAsync (sem streaks, correlação BFS,
/// supressão; incidente SOMENTE se Critical).
/// </summary>
public sealed class MonitoringEventIngestor(
    IMonitoringEventRepository eventRepository,
    IAlertRepository alertRepository,
    IIncidentRepository incidentRepository,
    IAlertEvaluator alertEvaluator,
    IUnitOfWork unitOfWork,
    ILogger<MonitoringEventIngestor> logger) : IMonitoringEventIngestor
{
    public async Task<MonitoringIngestResult> IngestAsync(
        ZabbixIngestInput input, CancellationToken cancellationToken = default)
        => input.IsRecovery
            ? await IngestRecoveryAsync(input, cancellationToken)
            : await IngestProblemAsync(input, cancellationToken);

    private async Task<MonitoringIngestResult> IngestProblemAsync(
        ZabbixIngestInput input, CancellationToken cancellationToken)
    {
        var eventId = input.EventId.Trim();

        // Dedup: row já gravado = replay do mesmo problem (eventid único na
        // fonte). Devolve o incidente vinculado (anti-storm idempotente).
        var existing = await eventRepository.GetBySourceAndExternalEventIdAsync(
            MonitoringSource.Zabbix, eventId, cancellationToken);
        if (existing is not null)
            return await DuplicateOutcomeAsync(eventId, cancellationToken);

        var occurredAt = input.OccurredAt
            ?? AppTimeProvider.Current.GetUtcNow().UtcDateTime;

        var monitoringEvent = MonitoringEvent.CreateProblem(
            MonitoringSource.Zabbix,
            eventId,
            input.DeviceId,
            checkType: null,
            input.Severity,
            occurredAt,
            input.Name,
            input.RawPayload);
        await eventRepository.AddAsync(monitoringEvent, cancellationToken);

        // F19-T003: o avaliador decide Alert (sempre), supressão (BFS) e
        // Incident (somente Critical) — idempotência por identidade.
        var evaluation = await alertEvaluator.EvaluateExternalAsync(
            monitoringEvent, input.Host, input.Ip, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (evaluation.Suppressed)
        {
            logger.LogInformation(
                "Alert {AlertId} do Zabbix (evento {EventId}) suprimido — vinculado ao incidente upstream {IncidentId}.",
                evaluation.AlertId, eventId, evaluation.IncidentId);
        }
        else if (evaluation.IncidentId is { } incidentId)
        {
            logger.LogInformation(
                "Incident {IncidentId} criado a partir do Zabbix (evento {EventId}, status PROBLEM).",
                incidentId, eventId);
        }
        else
        {
            logger.LogInformation(
                "Alert {AlertId} criado a partir do Zabbix (evento {EventId}) sem incidente (severidade não-Critical).",
                evaluation.AlertId, eventId);
        }

        return new(evaluation.AlertId, evaluation.IncidentId, IngestOutcome.Created);
    }

    private async Task<MonitoringIngestResult> IngestRecoveryAsync(
        ZabbixIngestInput input, CancellationToken cancellationToken)
    {
        var problemId = input.EventId.Trim();          // {EVENT.ID} = problem original
        var recoveryId = string.IsNullOrWhiteSpace(input.RecoveryEventId)
            ? null
            : input.RecoveryEventId.Trim();            // {EVENT.RECOVERY.ID}, opcional
        var occurredAt = input.OccurredAt
            ?? AppTimeProvider.Current.GetUtcNow().UtcDateTime;

        var problemRow = await eventRepository.GetBySourceAndExternalEventIdAsync(
            MonitoringSource.Zabbix, problemId, cancellationToken);

        if (problemRow is null)
            return await IngestOrphanRecoveryAsync(
                input, problemId, recoveryId, occurredAt, cancellationToken);

        // Replay de recovery (row do problem já Recovered): idempotente,
        // devolve o estado atual sem reescrever.
        if (problemRow.Status == MonitoringEventStatus.Recovered)
            return await ResolvedOutcomeAsync(problemId, cancellationToken);

        if (recoveryId is not null && !string.Equals(recoveryId, problemId, StringComparison.Ordinal))
        {
            // Moderno: linha própria para o recovery + backfill do par no
            // problem row (espelho de r_eventid — 100 → 150).
            var recoveryRow = MonitoringEvent.CreateRecovered(
                MonitoringSource.Zabbix,
                recoveryId,
                problemId,
                recoveryId,
                problemRow.DeviceId ?? input.DeviceId,
                problemRow.CheckType,
                input.Severity,
                occurredAt,
                input.Name,
                input.RawPayload);
            await eventRepository.AddAsync(recoveryRow, cancellationToken);
            problemRow.MarkRecovered(recoveryId, occurredAt);
        }
        else
        {
            // Legado (ação sem {EVENT.RECOVERY.ID}): transição in-place —
            // criar uma linha nova colidiria com a unicidade (eventid do
            // problem == eventid usado no OK).
            problemRow.MarkRecovered(problemId, occurredAt);
        }

        var alert = await alertRepository.GetByExternalAsync(
            MonitoringSource.Zabbix, problemId, cancellationToken);
        if (alert is null)
        {
            // Linha existe mas não há alerta (world pré-upgrade): só grava a
            // transição; incidentes legados sem vínculo ficam órfãos (nota do
            // ADR-027 — fechamento manual).
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "OK do Zabbix sem alerta correspondente (problem {ProblemId}) — transição gravada sem alert/incident.",
                problemId);
            return new(null, null, IngestOutcome.None);
        }

        alert.Resolve();

        // Fecha TODO incidente não-Resolved vinculado (Open, Investigating,
        // Identified, Monitoring) — o estado do ciclo não impede o recovery.
        var unresolvedIds = await incidentRepository.GetUnresolvedIdsLinkedToAlertAsync(
            alert.Id, cancellationToken);
        Guid? incidentId = unresolvedIds.Count > 0 ? unresolvedIds[0] : null;

        foreach (var id in unresolvedIds)
        {
            var incident = await incidentRepository.GetByIdAsync(id, cancellationToken);
            if (incident is null || incident.Status == IncidentStatus.Resolved)
                continue;

            // Regra 5 (revisão F19): fecha só quando TODOS os alerts
            // vinculados normalizaram — cobre o alert Suppressed (vítima)
            // cujo incidente upstream tem irmãos ainda abertos. O alert
            // resolvido agora conta como resolvido (GetAlertsAsync é
            // AsNoTracking e pode vir com o status antigo do banco).
            var linkedAlerts = await incidentRepository.GetAlertsAsync(
                incident.Id, cancellationToken);
            var allResolved = linkedAlerts.Count > 0
                && linkedAlerts.All(linked =>
                    linked.Id == alert.Id || linked.Status == AlertStatus.Resolved);

            if (allResolved)
                incident.ChangeStatus(IncidentStatus.Resolved);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Replay pós-resolução: devolve o incidente já fechado.
        incidentId ??= (await incidentRepository.GetLinkedIdsToAlertAsync(
            alert.Id, cancellationToken)).FirstOrDefault();

        return new(alert.Id, incidentId, IngestOutcome.Resolved);
    }

    /// <summary>
    /// OK sem problem row (evento nunca ingerido, ex.: ação legada pré-upgrade
    /// ou host fora do escopo): grava row de recovery como órfão e responde
    /// none — nunca derruba o webhook.
    /// </summary>
    private async Task<MonitoringIngestResult> IngestOrphanRecoveryAsync(
        ZabbixIngestInput input,
        string problemId,
        string? recoveryId,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        var rowId = recoveryId ?? problemId;
        var existing = await eventRepository.GetBySourceAndExternalEventIdAsync(
            MonitoringSource.Zabbix, rowId, cancellationToken);
        if (existing is not null)
            return new(null, null, IngestOutcome.None);

        var orphan = MonitoringEvent.CreateRecovered(
            MonitoringSource.Zabbix,
            rowId,
            problemId,
            recoveryId,
            input.DeviceId,
            checkType: null,
            input.Severity,
            occurredAt,
            input.Name,
            input.RawPayload);
        await eventRepository.AddAsync(orphan, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Recovery órfão do Zabbix: nenhum problem row para o evento {ProblemId} (recovery {RecoveryId}).",
            problemId, recoveryId ?? rowId);

        return new(null, null, IngestOutcome.None);
    }

    /// <summary>Replay de PROBLEM: row + alert existentes; incidente vinculado.</summary>
    private async Task<MonitoringIngestResult> DuplicateOutcomeAsync(
        string problemId, CancellationToken cancellationToken)
    {
        var alert = await alertRepository.GetByExternalAsync(
            MonitoringSource.Zabbix, problemId, cancellationToken);
        if (alert is null)
            return new(null, null, IngestOutcome.Duplicate);

        var unresolved = await incidentRepository.GetUnresolvedIdsLinkedToAlertAsync(
            alert.Id, cancellationToken);
        var incidentId = unresolved.Count > 0
            ? unresolved[0]
            : (await incidentRepository.GetLinkedIdsToAlertAsync(
                alert.Id, cancellationToken)).FirstOrDefault();

        return new(alert.Id, incidentId, IngestOutcome.Duplicate);
    }

    /// <summary>
    /// Replay de OK: estado atual (incidente preferencialmente não-Resolved).
    /// Sem alerta correspondente (row órfã/limpa) segue none — mesma resposta
    /// do primeiro OK sem problem.
    /// </summary>
    private async Task<MonitoringIngestResult> ResolvedOutcomeAsync(
        string problemId, CancellationToken cancellationToken)
    {
        var alert = await alertRepository.GetByExternalAsync(
            MonitoringSource.Zabbix, problemId, cancellationToken);
        if (alert is null)
            return new(null, null, IngestOutcome.None);

        var unresolved = await incidentRepository.GetUnresolvedIdsLinkedToAlertAsync(
            alert.Id, cancellationToken);
        var incidentId = unresolved.Count > 0
            ? unresolved[0]
            : (await incidentRepository.GetLinkedIdsToAlertAsync(
                alert.Id, cancellationToken)).FirstOrDefault();

        return new(alert.Id, incidentId, IngestOutcome.Resolved);
    }
}

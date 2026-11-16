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
    None,

    /// <summary>
    /// ADR-068: PROBLEM repetido com o problema lógico ainda aberto — o
    /// MonitoringEvent é gravado (histórico), mas nenhum Alert/Incident nem
    /// notificação nasce. Não é replay de eventId (esse é Duplicate).
    /// </summary>
    Suppressed
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
    string RawPayload,
    string? SourceContextJson = null);

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
    IMonitoringProblemStateRepository problemStateRepository,
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
        {
            if (EnrichMissingContext(existing, input.SourceContextJson))
                await unitOfWork.SaveChangesAsync(cancellationToken);
            return await DuplicateOutcomeAsync(eventId, cancellationToken);
        }

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
            input.RawPayload,
            input.SourceContextJson);
        await eventRepository.AddAsync(monitoringEvent, cancellationToken);

        // ADR-068: máquina de estados do problema lógico. PROBLEM repetido
        // com o problema ainda aberto (update/flap sem OK) grava só o
        // evento — nenhum Alert/Incident/domain event = zero notificação.
        var correlationKey = MonitoringCorrelationKey.Resolve(
            MonitoringSource.Zabbix, input.SourceContextJson,
            input.DeviceId, input.Host, input.Ip, input.Name);
        var problemState = await problemStateRepository
            .GetBySourceAndCorrelationKeyAsync(
                MonitoringSource.Zabbix, correlationKey, cancellationToken);

        var stateIsOpen = problemState?.Status
            is ProblemStatus.Alerta or ProblemStatus.Incidente;
        var escalates = problemState?.Status == ProblemStatus.Alerta
            && AlertEvaluator.MapAlertSeverity(input.Severity)
                == AlertSeverity.Critical;

        if (stateIsOpen && !escalates)
        {
            problemState!.RegisterRepeatedProblem(input.Severity, eventId);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "PROBLEM {EventId} repetido com o problema {CorrelationKey} ainda {Status} — " +
                "evento gravado, Alert/Incident/notificação suprimidos (repetição {Repeated}).",
                eventId, correlationKey, problemState.Status, problemState.RepeatedCount);
            return new(
                problemState.AlertId, problemState.IncidentId, IngestOutcome.Suppressed);
        }

        // F19-T003: o avaliador decide Alert (sempre), supressão (BFS) e
        // Incident (somente Critical) — idempotência por identidade.
        var evaluation = await alertEvaluator.EvaluateExternalAsync(
            monitoringEvent, input.Host, input.Ip, cancellationToken);

        var newStatus = evaluation.IncidentId is not null && !evaluation.Suppressed
            ? ProblemStatus.Incidente
            : ProblemStatus.Alerta;
        var cycleIncidentId = evaluation.Suppressed ? null : evaluation.IncidentId;
        var previousAlertId = problemState?.AlertId;

        if (problemState is null)
        {
            problemState = MonitoringProblemState.CreateOpen(
                MonitoringSource.Zabbix, correlationKey, input.DeviceId,
                input.Severity, newStatus, evaluation.AlertId, cycleIncidentId,
                eventId, occurredAt);
            await problemStateRepository.AddAsync(problemState, cancellationToken);
        }
        else if (problemState.Status == ProblemStatus.Resolvido)
        {
            problemState.Reopen(
                input.DeviceId, input.Severity, newStatus, evaluation.AlertId,
                cycleIncidentId, eventId, occurredAt);
        }
        else
        {
            // Escalonamento Alerta→Incidente (único caminho que chega ao
            // avaliador com estado aberto): assume os novos vínculos.
            problemState.UpdateOpen(
                input.DeviceId, input.Severity, newStatus, evaluation.AlertId,
                cycleIncidentId, eventId);
        }

        // O alerta do ciclo anterior é substituído pelo novo PROBLEM —
        // resolve para não deixar Warning órfão Open para sempre.
        if (previousAlertId is { } supersededId
            && supersededId != evaluation.AlertId)
        {
            var superseded = await alertRepository.GetByIdAsync(
                supersededId, cancellationToken);
            if (superseded?.Status
                is AlertStatus.Open or AlertStatus.Suppressed)
            {
                // F57: supersede por escalonamento não é resolução do
                // problema — resolve sem evento para não duplicar a
                // notificação ("Alerta resolvido" + "Incidente aberto"
                // para a mesma ocorrência no WhatsApp).
                superseded.Resolve(raiseResolvedEvent: false);
            }
        }

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
        {
            if (EnrichMissingContext(problemRow, input.SourceContextJson))
                await unitOfWork.SaveChangesAsync(cancellationToken);
            return await ResolvedOutcomeAsync(problemId, cancellationToken);
        }

        EnrichMissingContext(problemRow, input.SourceContextJson);

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
                input.RawPayload,
                input.SourceContextJson);
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

        // ADR-068: o OK encerra o ciclo do problema lógico — o Alert do
        // ciclo é a ponte (todo problem externo gera exatamente um).
        // Idempotente: replay de recovery não re-notifica (MarkResolved
        // é no-op em Resolvido e os domain events já não re-disparam).
        var problemState = await problemStateRepository.GetByAlertIdAsync(
            alert.Id, cancellationToken);
        problemState?.MarkResolved(occurredAt);

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
            input.RawPayload,
            input.SourceContextJson);
        await eventRepository.AddAsync(orphan, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Recovery órfão do Zabbix: nenhum problem row para o evento {ProblemId} (recovery {RecoveryId}).",
            problemId, recoveryId ?? rowId);

        return new(null, null, IngestOutcome.None);
    }

    private static bool EnrichMissingContext(
        MonitoringEvent monitoringEvent, string? sourceContextJson)
    {
        if (!string.IsNullOrWhiteSpace(monitoringEvent.SourceContextJson)
            || string.IsNullOrWhiteSpace(sourceContextJson))
            return false;

        monitoringEvent.SetSourceContextJson(sourceContextJson);
        return true;
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

using System.Diagnostics;
using System.Diagnostics.Metrics;
using InfraControl.Application.Notifications.Transitions.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Notifications.Transitions;

public interface IOccurrenceTransitionGate
{
    /// <summary>
    /// Compara o novo estado com o último estado persistido da ocorrência.
    /// true = transição legítima → notificação autorizada em TODOS os
    /// canais; false = mesmo estado → evento descartado antes da
    /// distribuição. Persiste o estado e grava o histórico da decisão.
    /// </summary>
    Task<bool> AuthorizeAsync(
        string occurrenceKey,
        OccurrenceState newState,
        string eventType,
        Guid sourceEventId,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Audita um evento de ocorrência descartado ANTES do gate (ex.:
    /// replay com eventId já marcado no inbox). Não altera estado nem
    /// autoriza notificação — só grava a decisão DuplicateEvent.
    /// </summary>
    Task RecordDiscardedAsync(
        string occurrenceKey,
        OccurrenceState? attemptedState,
        string eventType,
        Guid sourceEventId,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Gate central de decisão de notificação (spec de transições):
/// NÃO HOUVE MUDANÇA DE ESTADO = NENHUMA NOTIFICAÇÃO; HOUVE MUDANÇA =
/// PROCESSAR. Roda no consumer de eventos de domínio — antes do
/// fanout — então nenhum canal (WhatsApp, SignalR, Email, Telegram,
/// Webhook…) pode contornar a regra.
///
/// Auditoria (§3): cada avaliação grava uma linha append-only em
/// occurrence_transitions com Decision (TransitionAccepted / SameState /
/// ConcurrentConflict), From/ToState, SuppressionReason e TraceId —
/// descartes ficam auditáveis sem gerar notificação.
///
/// Concorrência: o UPDATE do estado carrega token de concorrência
/// (Version) e a chave é única; duas decisões simultâneas na mesma
/// ocorrência fazem uma perder o UPDATE/INSERT, desfazer o pendente,
/// reler o estado já avançado e descartar se igual ao recebido.
/// </summary>
public sealed class OccurrenceTransitionGate(
    IOccurrenceRepository occurrenceRepository,
    ILogger<OccurrenceTransitionGate> logger) : IOccurrenceTransitionGate
{
    private static readonly Meter Meter = new("InfraControl.Notifications.Transitions");

    /// <summary>Decisões do gate por resultado e estado de destino (§4).</summary>
    private static readonly Counter<long> Decisions = Meter.CreateCounter<long>(
        "infracontrol_occurrence_gate_decisions",
        description: "Decisões do gate de transição de estado por ocorrência.");

    public async Task<bool> AuthorizeAsync(
        string occurrenceKey,
        OccurrenceState newState,
        string eventType,
        Guid sourceEventId,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.TraceId.ToString();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var occurrence = await occurrenceRepository.GetByKeyAsync(
                occurrenceKey, cancellationToken);

            if (occurrence?.State == newState)
            {
                // SameState: descarta ANTES do fanout, mas audita a decisão.
                await RecordDecisionAsync(
                    occurrenceKey,
                    occurrence!.State,
                    newState,
                    OccurrenceDecision.SameState,
                    $"estado já persistido: {newState}",
                    eventType,
                    sourceEventId,
                    traceId,
                    correlationId,
                    cancellationToken);
                RecordMetric(OccurrenceDecision.SameState, newState);
                logger.LogInformation(
                    "Ocorrência {OccurrenceKey} já está em {State} — evento {EventType} descartado (sem mudança de estado).",
                    occurrenceKey, newState, eventType);
                return false;
            }

            var fromState = occurrence?.State;
            var added = occurrence is null;
            if (added)
            {
                occurrence = Occurrence.Create(occurrenceKey, newState);
                await occurrenceRepository.AddAsync(occurrence, cancellationToken);
            }
            else
            {
                occurrence!.TransitionTo(newState);
            }

            var transition = OccurrenceTransition.Create(
                occurrenceKey,
                fromState,
                newState,
                OccurrenceDecision.TransitionAccepted,
                eventType,
                sourceEventId,
                traceId: traceId,
                correlationId: correlationId);
            await occurrenceRepository.AddTransitionAsync(transition, cancellationToken);

            if (await occurrenceRepository.TrySaveAsync(cancellationToken))
            {
                RecordMetric(OccurrenceDecision.TransitionAccepted, newState);
                if (fromState is null)
                {
                    logger.LogInformation(
                        "Ocorrência {OccurrenceKey} criada em {State} por {EventType} — notificação autorizada.",
                        occurrenceKey, newState, eventType);
                }
                else
                {
                    logger.LogInformation(
                        "Ocorrência {OccurrenceKey}: {From} → {To} por {EventType} — notificação autorizada.",
                        occurrenceKey, fromState, newState, eventType);
                }

                return true;
            }

            // Conflito de concorrência: outra decisão avançou o estado —
            // desfaz os pendentes, audita o conflito e reavalia.
            logger.LogInformation(
                "Concorrência na ocorrência {OccurrenceKey} — reavaliando estado persistido.",
                occurrenceKey);
            if (added)
                occurrenceRepository.Remove(occurrence!);
            else
                await occurrenceRepository.ReloadAsync(occurrence!, cancellationToken);
            occurrenceRepository.RemoveTransition(transition);

            await RecordConflictAsync(
                occurrenceKey, fromState, newState,
                eventType, sourceEventId, traceId, correlationId, cancellationToken);
        }

        // Duas corridas seguidas: propaga para o retry do broker em vez
        // de descartar uma transição legítima.
        throw new InvalidOperationException(
            $"Não foi possível persistir a transição da ocorrência '{occurrenceKey}'.");
    }

    public async Task RecordDiscardedAsync(
        string occurrenceKey,
        OccurrenceState? attemptedState,
        string eventType,
        Guid sourceEventId,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        var persisted = await occurrenceRepository.GetByKeyAsync(
            occurrenceKey, cancellationToken);
        if (persisted is null)
            return; // sem ocorrência persistida não há o que correlacionar

        await RecordDecisionAsync(
            occurrenceKey,
            persisted.State,
            persisted.State,
            OccurrenceDecision.DuplicateEvent,
            $"eventId já processado no inbox (estado tentado: {attemptedState})",
            eventType,
            sourceEventId,
            traceId,
            correlationId,
            cancellationToken);
        RecordMetric(OccurrenceDecision.DuplicateEvent, persisted.State);
    }

    /// <summary>
    /// Grava a decisão de descarte (SameState) — auditável sem gerar
    /// notificação. Falha de persistência não altera o veredito: o
    /// estado persistido continua sendo a fonte da verdade.
    /// </summary>
    private async Task RecordDecisionAsync(
        string occurrenceKey,
        OccurrenceState fromState,
        OccurrenceState toState,
        OccurrenceDecision decision,
        string suppressionReason,
        string eventType,
        Guid sourceEventId,
        string? traceId,
        Guid? correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await occurrenceRepository.AddTransitionAsync(
                OccurrenceTransition.Create(
                    occurrenceKey, fromState, toState, decision,
                    eventType, sourceEventId, suppressionReason, traceId,
                    correlationId),
                cancellationToken);
            await occurrenceRepository.TrySaveAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Falha ao auditar decisão {Decision} da ocorrência {OccurrenceKey}.",
                decision, occurrenceKey);
        }
    }

    /// <summary>Auditoria best-effort do conflito de concorrência.</summary>
    private async Task RecordConflictAsync(
        string occurrenceKey,
        OccurrenceState? fromState,
        OccurrenceState attemptedState,
        string eventType,
        Guid sourceEventId,
        string? traceId,
        Guid? correlationId,
        CancellationToken cancellationToken)
    {
        RecordMetric(OccurrenceDecision.ConcurrentConflict, attemptedState);
        try
        {
            await occurrenceRepository.AddTransitionAsync(
                OccurrenceTransition.Create(
                    occurrenceKey, fromState, attemptedState,
                    OccurrenceDecision.ConcurrentConflict,
                    eventType, sourceEventId,
                    "decisão concorrente — estado reavaliado", traceId,
                    correlationId),
                cancellationToken);
            await occurrenceRepository.TrySaveAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "Falha ao auditar conflito de concorrência da ocorrência {OccurrenceKey}.",
                occurrenceKey);
        }
    }

    private static void RecordMetric(
        OccurrenceDecision decision, OccurrenceState? toState)
        => Decisions.Add(1,
            new KeyValuePair<string, object?>("decision", decision.ToString()),
            new KeyValuePair<string, object?>("to_state", toState?.ToString() ?? "none"));
}

using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Log de decisão do gate de transição de estado (auditoria §3):
/// append-only — transições aceitas (From→To distintos, decisão
/// TransitionAccepted) e descartes auditáveis (SameState/ConcurrentConflict,
/// FromState = ToState). FromState nulo = primeira observação da ocorrência.
/// </summary>
public sealed class OccurrenceTransition : Entity
{
    public const int KeyMaxLength = 200;
    public const int EventTypeMaxLength = 255;
    public const int SuppressionReasonMaxLength = 500;
    public const int TraceIdMaxLength = 64;

    public string OccurrenceKey { get; private set; } = string.Empty;
    public OccurrenceState? FromState { get; private set; }
    public OccurrenceState ToState { get; private set; }
    public OccurrenceDecision Decision { get; private set; }
    public string? SuppressionReason { get; private set; }
    public string? TraceId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public Guid SourceEventId { get; private set; }
    public Guid? CorrelationId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    private OccurrenceTransition() { }

    public static OccurrenceTransition Create(
        string occurrenceKey,
        OccurrenceState? fromState,
        OccurrenceState toState,
        OccurrenceDecision decision,
        string eventType,
        Guid sourceEventId,
        string? suppressionReason = null,
        string? traceId = null,
        Guid? correlationId = null)
        => new()
        {
            OccurrenceKey = occurrenceKey,
            FromState = fromState,
            ToState = toState,
            Decision = decision,
            SuppressionReason = suppressionReason,
            TraceId = traceId,
            EventType = eventType,
            SourceEventId = sourceEventId,
            CorrelationId = correlationId,
            OccurredAtUtc = AppTimeProvider.Current.GetUtcNow().UtcDateTime
        };
}

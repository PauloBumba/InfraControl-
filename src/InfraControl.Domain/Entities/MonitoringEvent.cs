using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Evento bruto de monitoramento de uma fonte externa (ADR-027): uma linha por
/// evento recebido, com unicidade (Source, ExternalEventId). O ciclo de vida do
/// problema vive em <see cref="ExternalProblemId"/> — no Zabbix, problem
/// (eventid 100) e recovery (eventid 150) são dois eventos distintos e só o
/// payload traz a correlação ({EVENT.ID}/{EVENT.RECOVERY.ID}); o par
/// problem→recovery é espelhado em <see cref="ExternalRecoveryId"/> do row do
/// problem (semântica de <c>r_eventid</c>). <see cref="Alert"/> referencia a
/// linha por <c>MonitoringEventId</c>.
/// </summary>
public sealed class MonitoringEvent : AggregateRoot
{
    public const int MessageMaxLength = 500;
    public const int ExternalIdMaxLength = 64;
    public const int CheckTypeMaxLength = 100;

    public MonitoringSource Source { get; private set; }
    public string ExternalEventId { get; private set; } = string.Empty;
    public string ExternalProblemId { get; private set; } = string.Empty;
    public string? ExternalRecoveryId { get; private set; }
    public Guid? DeviceId { get; private set; }
    public string? CheckType { get; private set; }
    public int Severity { get; private set; }
    public MonitoringEventStatus Status { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime? RecoveredAt { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public string? RawPayload { get; private set; }

    private MonitoringEvent() { }

    /// <summary>
    /// Evento de problema (Zabbix action PROBLEM): <c>ExternalProblemId</c> =
    /// próprio <paramref name="externalEventId"/>; recovery ainda desconhecido.
    /// </summary>
    public static MonitoringEvent CreateProblem(
        MonitoringSource source,
        string externalEventId,
        Guid? deviceId,
        string? checkType,
        int severity,
        DateTime occurredAt,
        string message,
        string? rawPayload)
        => new()
        {
            Source = source,
            ExternalEventId = ValidateExternalId(externalEventId, nameof(externalEventId)),
            ExternalProblemId = externalEventId.Trim(),
            DeviceId = deviceId,
            CheckType = NormalizeCheckType(checkType),
            Severity = ValidateSeverity(severity),
            Status = MonitoringEventStatus.Problem,
            OccurredAt = occurredAt,
            Message = NormalizeMessage(message),
            RawPayload = rawPayload
        };

    /// <summary>
    /// Evento de recuperação (Zabbix action OK com recoveryEventId): linha
    /// própria (Status=Recovered) apontando o problem de origem.
    /// </summary>
    public static MonitoringEvent CreateRecovered(
        MonitoringSource source,
        string externalEventId,
        string externalProblemId,
        string? externalRecoveryId,
        Guid? deviceId,
        string? checkType,
        int severity,
        DateTime occurredAt,
        string message,
        string? rawPayload)
    {
        var recoveryId = string.IsNullOrWhiteSpace(externalRecoveryId)
            ? externalEventId
            : externalRecoveryId;

        return new MonitoringEvent
        {
            Source = source,
            ExternalEventId = ValidateExternalId(externalEventId, nameof(externalEventId)),
            ExternalProblemId = ValidateExternalId(externalProblemId, nameof(externalProblemId)),
            ExternalRecoveryId = recoveryId.Trim(),
            DeviceId = deviceId,
            CheckType = NormalizeCheckType(checkType),
            Severity = ValidateSeverity(severity),
            Status = MonitoringEventStatus.Recovered,
            OccurredAt = occurredAt,
            RecoveredAt = occurredAt,
            Message = NormalizeMessage(message),
            RawPayload = rawPayload
        };
    }

    /// <summary>
    /// Problem → Recovered (backfill do par quando o recovery chega, ou
    /// transição in-place no legado sem recoveryEventId — aí
    /// <paramref name="externalRecoveryId"/> é o próprio id do problem).
    /// </summary>
    public void MarkRecovered(string externalRecoveryId, DateTime recoveredAt)
    {
        if (Status == MonitoringEventStatus.Recovered)
            return;

        if (string.IsNullOrWhiteSpace(externalRecoveryId))
            throw new ArgumentException("O id do evento de recuperação é obrigatório.", nameof(externalRecoveryId));

        Status = MonitoringEventStatus.Recovered;
        ExternalRecoveryId = externalRecoveryId.Trim();
        RecoveredAt = recoveredAt;
    }

    private static string ValidateExternalId(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("O id externo do evento é obrigatório.", paramName);
        if (value.Trim().Length > ExternalIdMaxLength)
            throw new ArgumentException(
                $"O id externo do evento excede {ExternalIdMaxLength} caracteres.", paramName);

        return value.Trim();
    }

    private static int ValidateSeverity(int severity)
    {
        if (severity < 0)
            throw new ArgumentException("A severidade não pode ser negativa.", nameof(severity));
        return severity;
    }

    private static string? NormalizeCheckType(string? checkType)
    {
        if (string.IsNullOrWhiteSpace(checkType))
            return null;

        var trimmed = checkType.Trim();
        return trimmed.Length > CheckTypeMaxLength
            ? trimmed[..CheckTypeMaxLength]
            : trimmed;
    }

    private static string NormalizeMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A mensagem do evento é obrigatória.", nameof(message));

        var trimmed = message.Trim();
        return trimmed.Length > MessageMaxLength
            ? trimmed[..MessageMaxLength]
            : trimmed;
    }
}

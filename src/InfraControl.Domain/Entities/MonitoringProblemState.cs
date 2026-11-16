using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// ADR-068: estado persistido do problema lógico de monitoramento — uma
/// linha por chave de correlação estável (triggerId do Zabbix ou fallback
/// device+nome), independente do eventid de cada PROBLEM/OK. É o que
/// permite notificar só em transição de estado: PROBLEM repetido com o
/// problema ainda aberto não gera Alert/Incident/notificação novos.
/// </summary>
public sealed class MonitoringProblemState : AggregateRoot
{
    public const int CorrelationKeyMaxLength = 160;
    public const int ExternalIdMaxLength = 64;

    public MonitoringSource Source { get; private set; }
    public string CorrelationKey { get; private set; } = string.Empty;
    public Guid? DeviceId { get; private set; }

    /// <summary>Severidade da fonte (Zabbix 0..5) do último PROBLEM visto.</summary>
    public int Severity { get; private set; }

    public ProblemStatus Status { get; private set; }

    /// <summary>Alert do ciclo atual (todo problem externo gera um).</summary>
    public Guid? AlertId { get; private set; }

    /// <summary>
    /// ADR-070/F58: Alert do ciclo anterior quando houve escalonamento
    /// Alerta→Incidente (UpdateOpen) — o consumer usa para cancelar as
    /// entregas pendentes do alerta substituído (uma ocorrência = uma
    /// classificação só).
    /// </summary>
    public Guid? PreviousAlertId { get; private set; }

    /// <summary>Incident próprio do ciclo atual (null em Alerta/suprimido).</summary>
    public Guid? IncidentId { get; private set; }

    /// <summary>Último eventid de PROBLEM associado a este estado.</summary>
    public string? LastProblemEventId { get; private set; }

    /// <summary>Ciclos de abertura (Resolvido → aberto incrementa).</summary>
    public int OpenCount { get; private set; }

    /// <summary>PROBLEMs repetidos suprimidos no ciclo atual (medida de flap).</summary>
    public int RepeatedCount { get; private set; }

    public DateTime OpenedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    private MonitoringProblemState() { }

    /// <summary>Primeira abertura do problema (Normal → Alerta/Incidente).</summary>
    public static MonitoringProblemState CreateOpen(
        MonitoringSource source,
        string correlationKey,
        Guid? deviceId,
        int severity,
        ProblemStatus status,
        Guid alertId,
        Guid? incidentId,
        string? problemEventId,
        DateTime openedAt)
    {
        var state = new MonitoringProblemState
        {
            Source = source,
            CorrelationKey = ValidateKey(correlationKey),
            DeviceId = deviceId,
            Severity = severity,
            Status = status,
            AlertId = alertId,
            IncidentId = incidentId,
            LastProblemEventId = NormalizeEventId(problemEventId),
            OpenCount = 1,
            RepeatedCount = 0,
            OpenedAt = openedAt,
            ResolvedAt = null
        };

        return state;
    }

    /// <summary>
    /// Resolvido → aberto de novo: novo ciclo (outros Alert/Incident),
    /// contadores rearmados, carimbo de resolução limpo.
    /// </summary>
    public void Reopen(
        Guid? deviceId,
        int severity,
        ProblemStatus status,
        Guid alertId,
        Guid? incidentId,
        string? problemEventId,
        DateTime openedAt)
    {
        if (Status != ProblemStatus.Resolvido)
            throw new InvalidOperationException(
                "Só um estado Resolvido pode reabrir um novo ciclo.");

        DeviceId = deviceId;
        Severity = severity;
        Status = status;
        AlertId = alertId;
        IncidentId = incidentId;
        PreviousAlertId = null;
        LastProblemEventId = NormalizeEventId(problemEventId);
        OpenCount++;
        RepeatedCount = 0;
        OpenedAt = openedAt;
        ResolvedAt = null;
    }

    /// <summary>
    /// PROBLEM repetido com o problema ainda aberto (Alerta/Incidente) —
    /// o evento é registrado como histórico, mas nenhum Alert/Incident nem
    /// notificação nasce. Atualiza severidade/último evento e conta a
    /// repetição (métrica de flap).
    /// </summary>
    public void RegisterRepeatedProblem(int severity, string? problemEventId)
    {
        if (Status == ProblemStatus.Resolvido)
            throw new InvalidOperationException(
                "Um problema Resolvido não registra repetição — reabre ciclo.");

        Severity = severity;
        LastProblemEventId = NormalizeEventId(problemEventId);
        RepeatedCount++;
    }

    /// <summary>
    /// Reavaliação de um problema aberto que chegou ao avaliador
    /// (escalonamento Alerta→Incidente): assume os novos vínculos e o
    /// novo status sem zerar o ciclo (OpenedAt/OpenCount preservados).
    /// </summary>
    public void UpdateOpen(
        Guid? deviceId,
        int severity,
        ProblemStatus status,
        Guid alertId,
        Guid? incidentId,
        string? problemEventId)
    {
        if (Status == ProblemStatus.Resolvido)
            throw new InvalidOperationException(
                "Um problema Resolvido não recebe atualização de ciclo aberto.");

        PreviousAlertId = AlertId;
        DeviceId = deviceId;
        Severity = severity;
        Status = status;
        AlertId = alertId;
        IncidentId = incidentId;
        LastProblemEventId = NormalizeEventId(problemEventId);
    }

    /// <summary>OK/recovery: ciclo encerra (idempotente).</summary>
    public void MarkResolved(DateTime resolvedAt)
    {
        if (Status == ProblemStatus.Resolvido)
            return;

        Status = ProblemStatus.Resolvido;
        ResolvedAt ??= resolvedAt;
    }

    private static string ValidateKey(string correlationKey)
    {
        if (string.IsNullOrWhiteSpace(correlationKey))
            throw new ArgumentException("A chave de correlação é obrigatória.", nameof(correlationKey));

        var trimmed = correlationKey.Trim();
        if (trimmed.Length > CorrelationKeyMaxLength)
            throw new ArgumentException(
                $"A chave de correlação excede {CorrelationKeyMaxLength} caracteres.", nameof(correlationKey));

        return trimmed;
    }

    private static string? NormalizeEventId(string? eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return null;

        var trimmed = eventId.Trim();
        return trimmed.Length > ExternalIdMaxLength
            ? trimmed[..ExternalIdMaxLength]
            : trimmed;
    }
}

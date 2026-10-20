using InfraControl.Domain.Common.Events;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Events;

/// <summary>
/// F33-T004/ADR-059 (decisões 2/3): alerta <b>Critical</b> criado (nativo
/// F6, prevenção F30 ou externo Zabbix ADR-027 — todos passam por
/// Alert.Create/CreateExternal). O NotificationEventConsumer avisa as
/// roles Operator e Admin; a segunda mensagem da decisão 3 ("Incidente
/// aberto") é o IncidentCreatedDomainEvent do incidente garantido no
/// MESMO fluxo (F6-T003 nativo / ADR-027 §3 externo) — por isso
/// <see cref="HasIncident"/> nasce <c>true</c>. Warning NÃO levanta;
/// Critical que nasce Suppressed (vítima de incidente upstream —
/// Alert.Suppress) também não: o grupo já recebe o incidente raiz.
/// </summary>
public sealed class AlertCreatedDomainEvent : DomainEvent
{
    public AlertCreatedDomainEvent(
        Guid alertId,
        AlertSeverity severity,
        Guid? deviceId,
        string message,
        bool hasIncident)
    {
        AlertId = alertId;
        Severity = severity;
        DeviceId = deviceId;
        Message = message;
        HasIncident = hasIncident;
    }

    public Guid AlertId { get; }
    public AlertSeverity Severity { get; }
    public Guid? DeviceId { get; }
    public string Message { get; }
    public bool HasIncident { get; }
    public override string EventType => nameof(AlertCreatedDomainEvent);
}

/// <summary>
/// F19-T007: alerta resolvido (Alert.Resolve — OK do Zabbix, comando da
/// API ou fechamento em lote). O NotificationEventConsumer avisa as roles
/// Operator e Admin; alerta suprimido/cancelado NÃO levanta (Resolve é o
/// único ponto de transição para Resolved).
/// </summary>
public sealed class AlertResolvedDomainEvent : DomainEvent
{
    public AlertResolvedDomainEvent(
        Guid alertId,
        string message,
        Guid? deviceId)
    {
        AlertId = alertId;
        Message = message;
        DeviceId = deviceId;
    }

    public Guid AlertId { get; }
    public string Message { get; }
    public Guid? DeviceId { get; }
    public override string EventType => nameof(AlertResolvedDomainEvent);
}

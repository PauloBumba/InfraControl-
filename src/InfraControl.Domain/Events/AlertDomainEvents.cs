using InfraControl.Domain.Common.Events;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Events;

/// <summary>
/// F33-T004/ADR-059 §decisões 2/3: alerta <b>Critical</b> criado (os 3
/// pontos de criação — Alert.Create nativo, CreateExternal Zabbix,
/// EvaluateVerdictAsync prevenção — passam pelas fábricas do Alert).
/// Warning e alerta que nasce Suppressed NÃO levantam (o incidente
/// upstream já foi notificado pelo IncidentCreatedDomainEvent). O
/// NotificationEventConsumer avisa Operator+Admin; a 2ª mensagem
/// "Incidente aberto" vem do próprio IncidentCreatedDomainEvent da
/// abertura — 2 mensagens distintas para o mesmo problema (decisão
/// explícita do usuário, ADR-059 §decisões 3).
/// </summary>
public sealed class AlertCreatedDomainEvent : DomainEvent
{
    public AlertCreatedDomainEvent(
        Guid alertId,
        AlertSeverity severity,
        Guid? deviceId,
        string message)
    {
        AlertId = alertId;
        Severity = severity;
        DeviceId = deviceId;
        Message = message;
    }

    public Guid AlertId { get; }
    public AlertSeverity Severity { get; }
    public Guid? DeviceId { get; }
    public string Message { get; }

    /// <summary>
    /// O alerta Critical acompanha um incidente (F6-T003/ADR-027). Nasce
    /// <c>false</c> na fábrica do Alert; o avaliador marca
    /// <c>true</c> depois de garantir o vínculo
    /// (<c>Alert.SetHasIncident</c>) — evento ainda não despachado
    /// nesse ponto (dispatch é no SaveChanges).
    /// </summary>
    public bool HasIncident { get; internal set; }

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

using InfraControl.Domain.Common.Events;

namespace InfraControl.Domain.Events;

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

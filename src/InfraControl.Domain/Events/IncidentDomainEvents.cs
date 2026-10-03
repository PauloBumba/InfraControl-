using InfraControl.Domain.Common.Events;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Events;

/// <summary>
/// F16-T002/ADR-023: incidente nasceu (criação manual F6, auto-abertura
/// F6 ou webhook Zabbix F16-T001 — todos passam por Incident.Create). O
/// NotificationEventConsumer transforma em notificação para as roles
/// Operator e Admin (decisão do usuário 2026-09-30).
/// </summary>
public sealed class IncidentCreatedDomainEvent : DomainEvent
{
    public IncidentCreatedDomainEvent(
        Guid incidentId,
        string title,
        IncidentSeverity severity,
        Guid? deviceId)
    {
        IncidentId = incidentId;
        Title = title;
        Severity = severity;
        DeviceId = deviceId;
    }

    public Guid IncidentId { get; }
    public string Title { get; }
    public IncidentSeverity Severity { get; }
    public Guid? DeviceId { get; }
    public override string EventType => nameof(IncidentCreatedDomainEvent);
}

/// <summary>
/// F19-T007: incidente resolvido (ChangeStatus → Resolved — comandos da
/// API, recuperação Zabbix ou outra fonte). O NotificationEventConsumer
/// avisa as roles Operator e Admin (regra 7 da revisão: só o InfraControl
/// decide a resolução; o n8n nunca manda nesse estado).
/// </summary>
public sealed class IncidentResolvedDomainEvent : DomainEvent
{
    public IncidentResolvedDomainEvent(
        Guid incidentId,
        string title,
        IncidentSeverity severity,
        Guid? deviceId)
    {
        IncidentId = incidentId;
        Title = title;
        Severity = severity;
        DeviceId = deviceId;
    }

    public Guid IncidentId { get; }
    public string Title { get; }
    public IncidentSeverity Severity { get; }
    public Guid? DeviceId { get; }
    public override string EventType => nameof(IncidentResolvedDomainEvent);
}

using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Condição detectada (glossário) — F6-T001: nasce Open na avaliação
/// pós-check e resolve automático quando normaliza; um alerta aberto por
/// (device, check) anti alert-storm (unicidade lógica no avaliador).
/// ADR-027: <see cref="Source"/> distingue o pipeline nativo do externo
/// (Zabbix); externo nasce de um <see cref="MonitoringEvent"/> com
/// <see cref="ExternalProblemId"/> — <see cref="DeviceId"/>/<see cref="Kind"/>
/// ficam nullable porque o host/trigger externo pode não mapear device nem
/// CheckKind nativo.
/// </summary>
public sealed class Alert : AggregateRoot
{
    /// <summary>Id externo do problem (Zabbix eventid) — máx. 64.</summary>
    public const int ExternalIdMaxLength = 64;

    public Guid? DeviceId { get; private set; }
    public CheckKind? Kind { get; private set; }
    public MonitoringSource Source { get; private set; }
    public Guid? MonitoringEventId { get; private set; }
    public string? ExternalProblemId { get; private set; }
    public AlertSeverity Severity { get; private set; }
    public AlertStatus Status { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public DateTime? ResolvedAt { get; private set; }

    private Alert() { }

    public static Alert Create(
        Guid deviceId,
        CheckKind kind,
        AlertSeverity severity,
        string message)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("O id do device é obrigatório.", nameof(deviceId));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A mensagem do alerta é obrigatória.", nameof(message));

        var alert = new Alert
        {
            DeviceId = deviceId,
            Kind = kind,
            Source = MonitoringSource.Native,
            Severity = severity,
            Status = AlertStatus.Open,
            Message = message.Trim()
        };

        // F33-T004/ADR-059: só Critical vira notificação "Alerta criado".
        if (severity == AlertSeverity.Critical)
            alert.RaiseDomainEvent(
                new AlertCreatedDomainEvent(alert.Id, severity, deviceId, alert.Message));

        return alert;
    }

    /// <summary>
    /// Alerta de fonte externa (ADR-027) — vinculado ao MonitoringEvent que
    /// originou o problem. Device/check opcionais (host não mapeado).
    /// </summary>
    public static Alert CreateExternal(
        Guid monitoringEventId,
        string externalProblemId,
        Guid? deviceId,
        CheckKind? kind,
        AlertSeverity severity,
        string message,
        MonitoringSource source)
    {
        if (monitoringEventId == Guid.Empty)
            throw new ArgumentException("O id do evento de monitoramento é obrigatório.", nameof(monitoringEventId));
        if (string.IsNullOrWhiteSpace(externalProblemId))
            throw new ArgumentException("O id externo do problem é obrigatório.", nameof(externalProblemId));
        if (externalProblemId.Trim().Length > ExternalIdMaxLength)
            throw new ArgumentException(
                $"O id externo do problem excede {ExternalIdMaxLength} caracteres.", nameof(externalProblemId));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A mensagem do alerta é obrigatória.", nameof(message));
        if (source == MonitoringSource.Native)
            throw new ArgumentException("A fonte de um alerta externa não pode ser Native.", nameof(source));

        var alert = new Alert
        {
            MonitoringEventId = monitoringEventId,
            ExternalProblemId = externalProblemId.Trim(),
            DeviceId = deviceId,
            Kind = kind,
            Source = source,
            Severity = severity,
            Status = AlertStatus.Open,
            Message = message.Trim()
        };

        // F33-T004/ADR-059: só Critical vira notificação "Alerta criado".
        if (severity == AlertSeverity.Critical)
            alert.RaiseDomainEvent(
                new AlertCreatedDomainEvent(alert.Id, severity, deviceId, alert.Message));

        return alert;
    }

    public void Resolve()
    {
        if (Status == AlertStatus.Resolved)
            return;

        Status = AlertStatus.Resolved;
        ResolvedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;

        // F19-T007: resolução → notificação Operator + Admin.
        RaiseDomainEvent(new AlertResolvedDomainEvent(Id, Message, DeviceId));
    }

    /// <summary>F6-T004: Open → Suppressed (upstream com incidente aberto).</summary>
    public void Suppress()
    {
        if (Status != AlertStatus.Open)
            return;

        Status = AlertStatus.Suppressed;

        // F33-T004/ADR-059: suprimido não notifica "Alerta criado" — o
        // incidente upstream já foi notificado pelo IncidentCreated.
        RemoveDomainEvents<AlertCreatedDomainEvent>();
    }

    /// <summary>
    /// F33-T004: o avaliador marca o vínculo de incidente no evento ainda
    /// pendente (dispatch dos domain events é no SaveChanges, posterior).
    /// No-op quando não há AlertCreated pendente (alerta já existente).
    /// </summary>
    public void SetHasIncident()
    {
        foreach (var domainEvent in DomainEvents.OfType<AlertCreatedDomainEvent>())
            domainEvent.HasIncident = true;
    }

    /// <summary>
    /// F6-T004: Suppressed → Open (upstream normalizou; a condição local
    /// persiste). Resolved nunca reabre — Resolve continua definitivo.
    /// </summary>
    public void Reopen()
    {
        if (Status == AlertStatus.Suppressed)
            Status = AlertStatus.Open;
    }
}

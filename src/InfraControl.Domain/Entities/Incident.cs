using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Incident (F6-T003): agrupa N Alertas (N:N via IncidentAlert); nasce
/// automático quando um Alert Critical abre (Ping down com streak) ou
/// manualmente; estados da UI (Open→Investigating→Identified→Monitoring→
/// Resolved com transições livres); 1 Incident Open por device
/// (anti-storm, decisão implícita da auto-abertura).
/// </summary>
public sealed class Incident : AggregateRoot
{
    public const int RootCauseMaxLength = 500;

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public IncidentSeverity Severity { get; private set; }
    public IncidentStatus Status { get; private set; }
    public Guid? DeviceId { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    /// <summary>
    /// F6-T005: causa provável derivada de topologia/alertas (heurística
    /// pt-BR); null = manual ou ainda não resolvida.
    /// </summary>
    public string? RootCause { get; private set; }

    private Incident() { }

    public static Incident Create(
        string title,
        IncidentSeverity severity,
        Guid? deviceId = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título do incidente é obrigatório.", nameof(title));

        var incident = new Incident
        {
            Title = title.Trim(),
            Description = description?.Trim(),
            Severity = severity,
            Status = IncidentStatus.Open,
            DeviceId = deviceId
        };

        // F16-T002: nasce → IncidentCreatedDomainEvent; o AppDbContext
        // despacha para o outbox na mesma transação (ADR-005) e o consumer
        // de notificações avisa Operator + Admin. Um único ponto de
        // levantamento cobre manual, webhook e auto-abertura.
        incident.RaiseDomainEvent(new IncidentCreatedDomainEvent(
            incident.Id, incident.Title, severity, deviceId));

        return incident;
    }

    public void ChangeStatus(IncidentStatus status)
    {
        if (Status == status)
            throw new ArgumentException(
                $"O incidente já está no status '{status}'.", nameof(status));

        Status = status;
        if (status == IncidentStatus.Resolved)
        {
            ResolvedAt ??= AppTimeProvider.Current.GetUtcNow().UtcDateTime;

            // F19-T007: resolução → notificação Operator + Admin (só o
            // InfraControl decide Incident = Resolved — regra 7).
            RaiseDomainEvent(new IncidentResolvedDomainEvent(
                Id, Title, Severity, DeviceId));
        }
        else
        {
            ResolvedAt = null;
        }

        RaiseDomainEvent(new IncidentUpdatedDomainEvent(Id, Status));
    }

    public void Acknowledge()
        => ChangeStatus(IncidentStatus.Investigating);

    /// <summary>
    /// F6-T005: causa provável (heurística). null/branco limpa; acima do
    /// limite → 400 pt-BR; redefinir o mesmo valor é no-op.
    /// </summary>
    public void SetRootCause(string? rootCause)
    {
        var trimmed = rootCause?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            RootCause = null;
            return;
        }

        if (trimmed.Length > RootCauseMaxLength)
            throw new ArgumentException(
                $"A causa provável deve ter no máximo {RootCauseMaxLength} caracteres.",
                nameof(rootCause));

        if (RootCause != trimmed)
            RootCause = trimmed;
    }
}

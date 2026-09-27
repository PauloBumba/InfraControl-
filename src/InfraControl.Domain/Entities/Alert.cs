using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Condição detectada (glossário) — F6-T001: nasce Open na avaliação
/// pós-check e resolve automático quando normaliza; um alerta aberto por
/// (device, check) anti alert-storm (unicidade lógica no avaliador).
/// </summary>
public sealed class Alert : AggregateRoot
{
    public Guid DeviceId { get; private set; }
    public CheckKind Kind { get; private set; }
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

        return new Alert
        {
            DeviceId = deviceId,
            Kind = kind,
            Severity = severity,
            Status = AlertStatus.Open,
            Message = message.Trim()
        };
    }

    public void Resolve()
    {
        if (Status == AlertStatus.Resolved)
            return;

        Status = AlertStatus.Resolved;
        ResolvedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
    }

    /// <summary>F6-T004: Open → Suppressed (upstream com incidente aberto).</summary>
    public void Suppress()
    {
        if (Status == AlertStatus.Open)
            Status = AlertStatus.Suppressed;
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

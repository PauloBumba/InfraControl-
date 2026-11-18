namespace InfraControl.Domain.Enums;

/// <summary>
/// Estados de NotificationDelivery (docs/07 — F9-T004; retry/DLQ em
/// F9-T009).
/// </summary>
public enum DeliveryStatus
{
    Pending = 1,
    Processing = 2,
    Sent = 3,
    Failed = 4,
    Cancelled = 5,
    /// <summary>Reservado: supressão auditável de delivery (ex.: janela de
    /// manutenção) — motivo em NotificationDelivery.SuppressionReason.</summary>
    Suppressed = 6
}

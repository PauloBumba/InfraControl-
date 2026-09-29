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
    Cancelled = 5
}

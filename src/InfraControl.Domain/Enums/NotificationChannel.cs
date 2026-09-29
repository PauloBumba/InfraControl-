namespace InfraControl.Domain.Enums;

/// <summary>
/// Canais de entrega de notificação (docs/07 — F9-T003+).
/// </summary>
public enum NotificationChannel
{
    SignalR = 1,
    Email = 2,
    Telegram = 3,
    WhatsApp = 4,
    Webhook = 5
}

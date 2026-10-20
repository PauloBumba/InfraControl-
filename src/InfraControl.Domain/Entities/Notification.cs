using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Notificação criada a partir de um domain event consumido (ADR-012,
/// docs/07 — F9-T001). Evento, Notification e Delivery são separados:
/// destinatários (F9-T002), preferências (F9-T003) e canais (F9-T004+)
/// vêm depois.
/// </summary>
public sealed class Notification : Entity
{
    public const int EventTypeMaxLength = 255;
    public const int TitleMaxLength = 200;
    public const int MessageMaxLength = 1000;

    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;

    /// <summary>
    /// F33-T005/ADR-059: device do evento de origem (Alert/Incident) —
    /// carregado até o worker de WhatsApp para buscar a série histórica
    /// no Zabbix e renderizar o gráfico da mensagem. Nulos para eventos
    /// sem device (Identity/PDI).
    /// </summary>
    public Guid? DeviceId { get; private set; }

    private Notification() { }

    public static Notification Create(
        Guid eventId, string eventType, string title, string message,
        Guid? deviceId = null)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("O evento de origem é obrigatório.", nameof(eventId));

        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("O tipo do evento é obrigatório.", nameof(eventType));

        if (eventType.Trim().Length > EventTypeMaxLength)
            throw new ArgumentException(
                $"O tipo do evento deve ter no máximo {EventTypeMaxLength} caracteres.", nameof(eventType));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título da notificação é obrigatório.", nameof(title));

        if (title.Trim().Length > TitleMaxLength)
            throw new ArgumentException(
                $"O título deve ter no máximo {TitleMaxLength} caracteres.", nameof(title));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A mensagem da notificação é obrigatória.", nameof(message));

        if (message.Trim().Length > MessageMaxLength)
            throw new ArgumentException(
                $"A mensagem deve ter no máximo {MessageMaxLength} caracteres.", nameof(message));

        return new Notification
        {
            EventId = eventId,
            EventType = eventType.Trim(),
            Title = title.Trim(),
            Message = message.Trim(),
            DeviceId = deviceId
        };
    }
}

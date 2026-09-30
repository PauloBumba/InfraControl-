using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Entrega de uma notificação a um usuário por um canal (F9-T004,
/// docs/07: Evento → Notification → NotificationDelivery → Canal).
/// O worker do canal lê `Pending`, envia e marca `Sent`/`Failed`.
/// </summary>
public sealed class NotificationDelivery : Entity
{
    public const int LastErrorMaxLength = 500;

    public Guid NotificationId { get; private set; }
    public Guid UserId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public DateTime? SentAt { get; private set; }
    public string? LastError { get; private set; }

    /// <summary>Tentativas já feitas nesta delivery (F9-T009).</summary>
    public int AttemptCount { get; private set; }

    /// <summary>
    /// Próxima tentativa agendada; nula = sem retry (viva = ainda não
    /// falhou, morta = esgotou as tentativas e entrou na DLQ).
    /// </summary>
    public DateTime? NextAttemptAt { get; private set; }

    private NotificationDelivery() { }

    public static NotificationDelivery Create(
        Guid notificationId,
        Guid userId,
        NotificationChannel channel,
        DeliveryStatus status = DeliveryStatus.Pending)
    {
        if (notificationId == Guid.Empty)
            throw new ArgumentException(
                "O id da notificação é obrigatório.", nameof(notificationId));

        if (userId == Guid.Empty)
            throw new ArgumentException("O id do usuário é obrigatório.", nameof(userId));

        if (!Enum.IsDefined(channel))
            throw new ArgumentException(
                $"Canal de notificação inválido: '{channel}'.", nameof(channel));

        if (!Enum.IsDefined(status))
            throw new ArgumentException(
                $"Status de entrega inválido: '{status}'.", nameof(status));

        return new NotificationDelivery
        {
            NotificationId = notificationId,
            UserId = userId,
            Channel = channel,
            Status = status
        };
    }

    public void MarkSent(DateTime sentAt)
    {
        Status = DeliveryStatus.Sent;
        SentAt = sentAt;
        LastError = null;
        NextAttemptAt = null;
    }

    /// <summary>
    /// F9-T009: registra a falha e agenda o próximo retry com backoff
    /// exponencial (base dobrando a cada tentativa). Ao esgotar
    /// <paramref name="maxAttempts"/> não agenda mais — a delivery entra
    /// na DLQ (Failed sem próximo retry) e o worker não a volta a ler.
    /// </summary>
    public void RegisterFailedAttempt(
        string error, DateTime now, int maxAttempts, TimeSpan backoff)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(
                nameof(maxAttempts), "O máximo de tentativas deve ser ao menos 1.");

        MarkFailed(error);

        AttemptCount++;

        if (AttemptCount >= maxAttempts)
        {
            NextAttemptAt = null;
            return;
        }

        var factor = 1L << (AttemptCount - 1);
        NextAttemptAt = now + TimeSpan.FromTicks(backoff.Ticks * factor);
    }

    public void MarkFailed(string error)
    {
        Status = DeliveryStatus.Failed;

        var trimmed = error.Trim();
        LastError = trimmed.Length > LastErrorMaxLength
            ? trimmed[..LastErrorMaxLength]
            : trimmed;
    }
}

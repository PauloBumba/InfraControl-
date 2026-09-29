using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Preferência de notificação por usuário e canal (F9-T003, docs/07):
/// desliga/liga a entrega em um canal. Uma por (usuário, canal).
/// </summary>
public sealed class NotificationPreference : Entity
{
    public Guid UserId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public bool Enabled { get; private set; }

    private NotificationPreference() { }

    public static NotificationPreference Create(
        Guid userId, NotificationChannel channel, bool enabled)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("O id do usuário é obrigatório.", nameof(userId));

        if (!Enum.IsDefined(channel))
            throw new ArgumentException(
                $"Canal de notificação inválido: '{channel}'.", nameof(channel));

        return new NotificationPreference
        {
            UserId = userId,
            Channel = channel,
            Enabled = enabled
        };
    }

    public void SetEnabled(bool enabled) => Enabled = enabled;
}

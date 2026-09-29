using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Destinatário de uma notificação (F9-T002, ADR-012/docs/07): usuário
/// direto OU role — exatamente um dos dois. Usuários presentes em
/// múltiplas roles recebem uma única notificação lógica (dedup na
/// resolução role → usuários).
/// </summary>
public sealed class NotificationRecipient : Entity
{
    public Guid NotificationId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Role { get; private set; }

    private NotificationRecipient() { }

    public static NotificationRecipient CreateForUser(
        Guid notificationId, Guid userId)
    {
        if (notificationId == Guid.Empty)
            throw new ArgumentException(
                "O id da notificação é obrigatório.", nameof(notificationId));

        if (userId == Guid.Empty)
            throw new ArgumentException("O id do usuário é obrigatório.", nameof(userId));

        return new NotificationRecipient
        {
            NotificationId = notificationId,
            UserId = userId
        };
    }

    public static NotificationRecipient CreateForRole(
        Guid notificationId, UserRole role)
    {
        if (notificationId == Guid.Empty)
            throw new ArgumentException(
                "O id da notificação é obrigatório.", nameof(notificationId));

        return new NotificationRecipient
        {
            NotificationId = notificationId,
            Role = role.ToString()
        };
    }

    public static NotificationRecipient CreateForRole(
        Guid notificationId, string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            throw new ArgumentException("O nome da role é obrigatório.", nameof(roleName));

        if (!Enum.TryParse<UserRole>(roleName, ignoreCase: true, out var role))
            throw new ArgumentException(
                $"Role inválida: '{roleName}'.", nameof(roleName));

        return CreateForRole(notificationId, role);
    }
}

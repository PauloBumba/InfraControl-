namespace InfraControl.Application.Notifications.Recipients;

/// <summary>
/// Payload de NotificationRecipient devolvido pela API (F9-T002).
/// </summary>
public sealed record NotificationRecipientDto(
    Guid Id,
    Guid NotificationId,
    Guid? UserId,
    string? Role);

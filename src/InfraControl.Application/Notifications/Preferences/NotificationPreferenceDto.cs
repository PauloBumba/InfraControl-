using InfraControl.Domain.Enums;

namespace InfraControl.Application.Notifications.Preferences;

/// <summary>
/// Payload de NotificationPreference devolvido pela API (F9-T003).
/// </summary>
public sealed record NotificationPreferenceDto(
    Guid UserId,
    NotificationChannel Channel,
    bool Enabled);

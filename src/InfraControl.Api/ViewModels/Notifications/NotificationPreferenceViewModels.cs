using InfraControl.Domain.Enums;

namespace InfraControl.Api.ViewModels.Notifications;

/// <summary>Upsert de preferência de notificação (F9-T003).</summary>
public sealed record SetNotificationPreferenceViewModel(
    Guid UserId,
    NotificationChannel Channel,
    bool Enabled);

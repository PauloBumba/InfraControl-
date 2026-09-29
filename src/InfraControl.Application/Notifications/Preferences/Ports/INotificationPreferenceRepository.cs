using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Notifications.Preferences.Ports;

public interface INotificationPreferenceRepository : IRepository<NotificationPreference>
{
    /// <summary>Preferências do usuário (canal ordenado); 404 se o usuário não existe.</summary>
    Task<IReadOnlyCollection<NotificationPreference>> ListByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Preferência (usuário, canal) ou null.</summary>
    Task<NotificationPreference?> FindAsync(
        Guid userId,
        NotificationChannel channel,
        CancellationToken cancellationToken = default);

    Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

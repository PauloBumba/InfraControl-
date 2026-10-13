using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Account.Settings.Ports;

public interface IUserSettingsRepository : IRepository<UserSettings>
{
    /// <summary>Settings do usuário ou null (lazy-create no command).</summary>
    Task<UserSettings?> FindByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

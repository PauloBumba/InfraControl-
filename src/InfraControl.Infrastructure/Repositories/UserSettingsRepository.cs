using InfraControl.Application.Account.Settings.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class UserSettingsRepository(AppDbContext dbContext)
    : IUserSettingsRepository
{
    public async Task AddAsync(
        UserSettings settings, CancellationToken cancellationToken = default)
    {
        await dbContext.UserSettings.AddAsync(settings, cancellationToken);
    }

    public Task<UserSettings?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.UserSettings
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public void Remove(UserSettings settings)
        => dbContext.UserSettings.Remove(settings);

    // Sem AsNoTracking: o command precisa de tracking para o update; o query
    // só lê campos (tracking inofensivo).
    public Task<UserSettings?> FindByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default)
        => dbContext.UserSettings
            .SingleOrDefaultAsync(s => s.UserId == userId, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await dbContext.SaveChangesAsync(cancellationToken);
}

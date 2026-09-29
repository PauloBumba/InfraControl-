using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Notifications.Preferences.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class NotificationPreferenceRepository(AppDbContext dbContext)
    : INotificationPreferenceRepository
{
    public async Task AddAsync(
        NotificationPreference preference, CancellationToken cancellationToken = default)
    {
        await dbContext.NotificationPreferences.AddAsync(preference, cancellationToken);
    }

    public Task<NotificationPreference?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.NotificationPreferences
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void Remove(NotificationPreference preference)
        => dbContext.NotificationPreferences.Remove(preference);

    public async Task<IReadOnlyCollection<NotificationPreference>> ListByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!await UserExistsAsync(userId, cancellationToken))
            throw new KeyNotFoundException($"Usuário '{userId}' não encontrado.");

        return await dbContext.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Channel)
            .ToListAsync(cancellationToken);
    }

    public Task<NotificationPreference?> FindAsync(
        Guid userId,
        NotificationChannel channel,
        CancellationToken cancellationToken = default)
        => dbContext.NotificationPreferences
            .SingleOrDefaultAsync(
                p => p.UserId == userId && p.Channel == channel, cancellationToken);

    public Task<bool> UserExistsAsync(
        Guid userId, CancellationToken cancellationToken = default)
        => dbContext.DomainUsers.AnyAsync(u => u.Id == userId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

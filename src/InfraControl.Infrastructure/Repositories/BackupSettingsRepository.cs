using InfraControl.Application.Operations.Backup.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>Singleton de backup editável pela plataforma.</summary>
public sealed class BackupSettingsRepository(AppDbContext dbContext)
    : IBackupSettingsRepository
{
    public Task<BackupSettings?> GetAsync(CancellationToken cancellationToken = default) =>
        dbContext.BackupSettings
            .OrderBy(s => s.CreateDateTime)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<BackupSettings?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        dbContext.BackupSettings.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task AddAsync(
        BackupSettings entity, CancellationToken cancellationToken = default) =>
        await dbContext.BackupSettings.AddAsync(entity, cancellationToken);

    public void Remove(BackupSettings entity) =>
        dbContext.BackupSettings.Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

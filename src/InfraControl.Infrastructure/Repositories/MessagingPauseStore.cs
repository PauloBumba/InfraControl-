using InfraControl.Application.Messaging.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>Store EF do estado de pause broker-level (F55-T003).</summary>
public sealed class MessagingPauseStore(AppDbContext dbContext) : IMessagingPauseStore
{
    public Task<MessagingQueuePause?> GetAsync(
        string queueName, CancellationToken cancellationToken = default)
        => dbContext.MessagingQueuePauses.AsNoTracking()
            .FirstOrDefaultAsync(p => p.QueueName == queueName, cancellationToken);

    public async Task<IReadOnlyList<MessagingQueuePause>> ListAsync(
        CancellationToken cancellationToken = default)
        => await dbContext.MessagingQueuePauses.AsNoTracking()
            .OrderBy(p => p.QueueName)
            .ToListAsync(cancellationToken);

    public async Task UpsertAsync(
        MessagingQueuePause pause, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.MessagingQueuePauses
            .FirstOrDefaultAsync(p => p.QueueName == pause.QueueName, cancellationToken);
        if (existing is null)
        {
            dbContext.MessagingQueuePauses.Add(pause);
        }
        else
        {
            dbContext.MessagingQueuePauses.Remove(existing);
            dbContext.MessagingQueuePauses.Add(pause);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        string queueName, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.MessagingQueuePauses
            .FirstOrDefaultAsync(p => p.QueueName == queueName, cancellationToken);
        if (existing is not null)
        {
            dbContext.MessagingQueuePauses.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

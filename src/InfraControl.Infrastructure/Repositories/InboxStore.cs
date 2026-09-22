using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

public sealed class InboxStore(AppDbContext dbContext) : IInboxStore
{
    public async Task<bool> TryMarkProcessedAsync(
        Guid eventId,
        string consumer,
        CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty) throw new ArgumentException("Event id is required.", nameof(eventId));
        if (string.IsNullOrWhiteSpace(consumer)) throw new ArgumentException("Consumer is required.", nameof(consumer));

        var alreadyProcessed = await dbContext.ProcessedMessages
            .AnyAsync(message => message.EventId == eventId && message.Consumer == consumer, cancellationToken);
        if (alreadyProcessed)
            return false;

        dbContext.ProcessedMessages.Add(new ProcessedMessage(eventId, consumer.Trim()));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            return await dbContext.ProcessedMessages
                .AnyAsync(message => message.EventId == eventId && message.Consumer == consumer, cancellationToken);
        }
    }
}

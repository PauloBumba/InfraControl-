using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Outbox;
using InfraControl.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Common.Messaging;

public class OutboxRepository : IOutboxRepository
{
    private readonly AppDbContext _context;

    public OutboxRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await _context.OutboxMessages.AddAsync(message, cancellationToken);
    }

    public async Task<List<OutboxMessage>> GetPendingMessagesAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        return await _context.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < m.MaxRetries)
            .OrderBy(m => m.OccurredAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await _context.OutboxMessages.FindAsync([messageId], cancellationToken);
        if (message != null)
        {
            message.MarkProcessed();
        }
    }

    public async Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        var message = await _context.OutboxMessages.FindAsync([messageId], cancellationToken);
        if (message != null)
        {
            message.MarkFailed(error);
        }
    }

    public async Task<bool> IsProcessedAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
    {
        return await _context.ProcessedMessages
            .AnyAsync(p => p.EventId == eventId && p.Consumer == consumer, cancellationToken);
    }
}

using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Repositories;

public sealed class CleanupService(AppDbContext dbContext, IOptions<RetentionOptions> options) : ICleanupService
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly RetentionOptions _options = options.Value;

    public async Task<CleanupResult> CleanupAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var batchSize = Math.Max(1, _options.BatchSize);

        var outboxDeleted = await DeleteOutboxAsync(_options.OutboxCutoff(utcNow), batchSize, cancellationToken);
        var processedDeleted = await DeleteProcessedAsync(_options.ProcessedCutoff(utcNow), batchSize, cancellationToken);
        var auditDeleted = await DeleteAuditAsync(_options.AuditCutoff(utcNow), batchSize, cancellationToken);

        return new CleanupResult(outboxDeleted, processedDeleted, auditDeleted);
    }

    private async Task<int> DeleteOutboxAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.OutboxMessages
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .OrderBy(m => m.ProcessedAt)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        return await _dbContext.OutboxMessages
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<int> DeleteProcessedAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.ProcessedMessages
            .Where(m => m.ProcessedAt < cutoff)
            .OrderBy(m => m.ProcessedAt)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        return await _dbContext.ProcessedMessages
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<int> DeleteAuditAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.AuditEntries
            .Where(m => m.OccurredAt < cutoff)
            .OrderBy(m => m.OccurredAt)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        return await _dbContext.AuditEntries
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }
}

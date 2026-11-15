using System.Text.Json;
using System.Text.RegularExpressions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Messaging.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F55-T004: queries paginadas do Message Explorer (outbox/deliveries).</summary>
public sealed class MessagingExplorerStore(AppDbContext dbContext)
    : IMessagingExplorerStore
{
    public async Task<PagedResult<OutboxMessage>> SearchOutboxAsync(
        MessagingMessageFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.OutboxMessages.AsNoTracking().AsQueryable();

        if (filter.MessageId is { } id) query = query.Where(m => m.Id == id);
        if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
            query = query.Where(m => m.CorrelationId == filter.CorrelationId.Trim());
        if (!string.IsNullOrWhiteSpace(filter.EventType))
            query = query.Where(m => m.EventType.Contains(filter.EventType.Trim()));
        if (filter.FromUtc is { } from) query = query.Where(m => m.OccurredAt >= from);
        if (filter.ToUtc is { } to) query = query.Where(m => m.OccurredAt <= to);
        if (filter.MinAttempts is { } min) query = query.Where(m => m.RetryCount >= min);

        query = filter.State?.Trim().ToLowerInvariant() switch
        {
            "pending" => query.Where(m => m.ProcessedAt == null && m.RetryCount < m.MaxRetries),
            "processed" => query.Where(m => m.ProcessedAt != null),
            "failed" => query.Where(m => m.ProcessedAt == null && m.Error != null),
            "exhausted" => query.Where(m => m.ProcessedAt == null && m.RetryCount >= m.MaxRetries),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var text = filter.Text.Trim();
            query = query.Where(m =>
                m.Payload.Contains(text) || m.EventType.Contains(text)
                || (m.CorrelationId != null && m.CorrelationId.Contains(text)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(m => m.OccurredAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<OutboxMessage>(items, total, filter.Page, filter.PageSize);
    }

    public Task<OutboxMessage?> GetOutboxAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.OutboxMessages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ProcessedMessage>> GetInboxByEventIdAsync(
        Guid eventId, CancellationToken cancellationToken = default)
        => await dbContext.ProcessedMessages.AsNoTracking()
            .Where(m => m.EventId == eventId)
            .OrderBy(m => m.ProcessedAt)
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<NotificationDelivery>> SearchDeliveriesAsync(
        MessagingDeliveryFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.NotificationDeliveries.AsNoTracking().AsQueryable();

        if (filter.DeliveryId is { } id) query = query.Where(d => d.Id == id);
        if (filter.NotificationId is { } nid) query = query.Where(d => d.NotificationId == nid);
        if (filter.MinAttempts is { } min) query = query.Where(d => d.AttemptCount >= min);
        if (filter.FromUtc is { } from) query = query.Where(d => d.CreateDateTime >= from);
        if (filter.ToUtc is { } to) query = query.Where(d => d.CreateDateTime <= to);

        if (!string.IsNullOrWhiteSpace(filter.Channel)
            && Enum.TryParse<Domain.Enums.NotificationChannel>(filter.Channel, true, out var channel))
        {
            query = query.Where(d => d.Channel == channel);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status)
            && Enum.TryParse<Domain.Enums.DeliveryStatus>(filter.Status, true, out var status))
        {
            query = query.Where(d => d.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(d => d.CreateDateTime)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationDelivery>(items, total, filter.Page, filter.PageSize);
    }

    public Task<NotificationDelivery?> GetDeliveryAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.NotificationDeliveries.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<PagedResult<NotificationDelivery>> SearchFailureDeliveriesAsync(
        MessagingDeliveryFilter filter, bool deadLetter,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.NotificationDeliveries.AsNoTracking()
            .Where(d => d.Status == Domain.Enums.DeliveryStatus.Failed);

        query = deadLetter
            ? query.Where(d => d.NextAttemptAt == null)
            : query.Where(d => d.NextAttemptAt != null);

        if (filter.DeliveryId is { } id) query = query.Where(d => d.Id == id);
        if (filter.NotificationId is { } nid) query = query.Where(d => d.NotificationId == nid);
        if (filter.MinAttempts is { } min) query = query.Where(d => d.AttemptCount >= min);
        if (filter.FromUtc is { } from) query = query.Where(d => d.CreateDateTime >= from);
        if (filter.ToUtc is { } to) query = query.Where(d => d.CreateDateTime <= to);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(d => deadLetter ? d.UpdateDateTime : d.NextAttemptAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationDelivery>(items, total, filter.Page, filter.PageSize);
    }
}

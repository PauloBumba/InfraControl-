using System.Text.Json;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Common.Events;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using MediatR;

namespace InfraControl.Infrastructure.Services;

public class DomainEventService : IDomainEventService
{
    private readonly IPublisher _publisher;
    private readonly AppDbContext _context;
    private readonly ICurrentCorrelation? _correlation;

    public DomainEventService(IPublisher publisher, AppDbContext context, ICurrentCorrelation? correlation = null)
    {
        _publisher = publisher;
        _context = context;
        _correlation = correlation;
    }

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), OutboxJson.Options);

        var outboxMessage = new OutboxMessage(
            domainEvent.EventType,
            payload,
            domainEvent.OccurredAt,
            correlationId: _correlation?.Id);

        await _context.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
    }
}

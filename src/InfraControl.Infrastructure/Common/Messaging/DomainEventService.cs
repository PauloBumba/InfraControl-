using System.Text.Json;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Common.Events;
using InfraControl.Domain.Outbox;
using InfraControl.Infrastructure.Common.Persistence;
using MediatR;

namespace InfraControl.Infrastructure.Common.Messaging;

public class DomainEventService : IDomainEventService
{
    private readonly IPublisher _publisher;
    private readonly AppDbContext _context;

    public DomainEventService(IPublisher publisher, AppDbContext context)
    {
        _publisher = publisher;
        _context = context;
    }

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());

        var outboxMessage = new OutboxMessage(
            domainEvent.EventType,
            payload,
            domainEvent.OccurredAt);

        await _context.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
    }
}

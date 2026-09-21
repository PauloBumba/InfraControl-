using InfraControl.Domain.Common.Entities;
using MediatR;

namespace InfraControl.Infrastructure.Common.Persistence;

public static class MediatorExtension
{
    public static async Task DispatchDomainEventsAsync(this IMediator mediator, AppDbContext context)
    {
        var aggregateRoots = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Any())
            .ToList();

        var domainEvents = aggregateRoots
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        aggregateRoots.ForEach(e => e.Entity.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
        {
            await mediator.Publish(domainEvent);
        }
    }
}

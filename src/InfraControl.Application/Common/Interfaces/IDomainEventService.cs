using InfraControl.Domain.Common.Events;

namespace InfraControl.Application.Common.Interfaces;

public interface IDomainEventService
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}

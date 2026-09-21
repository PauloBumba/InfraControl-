using InfraControl.Application.Common.Interfaces;
using MassTransit;

namespace InfraControl.Infrastructure.Services;

public class MassTransitEventBus : IEventBus
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitEventBus(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class
    {
        await _publishEndpoint.Publish(message, cancellationToken);
    }

    public Task PublishAsync(object message, Type messageType, CancellationToken cancellationToken = default)
        => _publishEndpoint.Publish(message, messageType, cancellationToken);
}

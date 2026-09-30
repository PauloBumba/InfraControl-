using InfraControl.Application.Common.Interfaces;
using MassTransit;

namespace InfraControl.Infrastructure.Services;

public class MassTransitEventBus : IEventBus
{
    /// <summary>
    /// Nome do header do correlation id. Precisa ser o MESMO string no
    /// publicador e no consumidor, e é o que o admin digita para achar o rastro.
    /// </summary>
    public const string CorrelationHeader = "CorrelationId";

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

    /// <param name="correlationId">
    /// ADR-021: vira header da mensagem, para o worker que consome logar sob o
    /// mesmo id da requisição que originou o evento.
    /// </param>
    public Task PublishAsync(object message, Type messageType, CancellationToken cancellationToken = default, string? correlationId = null)
    {
        if (correlationId is null)
            return _publishEndpoint.Publish(message, messageType, cancellationToken);

        // MassTransit não tem overload "publique com estes headers" para
        // object — tem pipe. Headers mora no SendContext, base do PublishContext.
        return _publishEndpoint.Publish(
            message,
            messageType,
            MassTransit.Pipe.Execute<MassTransit.PublishContext>(context =>
                context.Headers.Set(CorrelationHeader, correlationId)),
            cancellationToken);
    }
}
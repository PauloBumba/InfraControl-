using InfraControl.Domain.Common.Events;

namespace InfraControl.Application.Common.Interfaces;

public interface IEventBus
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class;

    /// <param name="correlationId">
    /// ADR-021: vira header da mensagem, para o worker que consome logar sob o
    /// mesmo id da requisição que originou o evento.
    /// </param>
    Task PublishAsync(object message, Type messageType, CancellationToken cancellationToken = default, string? correlationId = null);
}

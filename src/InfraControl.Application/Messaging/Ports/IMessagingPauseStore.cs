using InfraControl.Domain.Entities;

namespace InfraControl.Application.Messaging.Ports;

/// <summary>
/// F55-T003: persistência do estado de pause broker-level — fila → conexões
/// a reciclar no resume. Sobrevive a restart da API.
/// </summary>
public interface IMessagingPauseStore
{
    Task<MessagingQueuePause?> GetAsync(
        string queueName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessagingQueuePause>> ListAsync(
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        MessagingQueuePause pause, CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string queueName, CancellationToken cancellationToken = default);
}

namespace InfraControl.Application.Common.Interfaces;

public interface IInboxStore
{
    Task<bool> TryMarkProcessedAsync(
        Guid eventId,
        string consumer,
        CancellationToken cancellationToken = default);
}

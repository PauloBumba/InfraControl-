namespace InfraControl.Application.Common.Interfaces;

public interface ICacheService : InfraControl.BuildingBlocks.Infrastructure.ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
}

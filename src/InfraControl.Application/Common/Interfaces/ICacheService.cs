namespace InfraControl.Application.Common.Interfaces;

public interface ICacheService : InfraControl.BuildingBlocks.Infrastructure.ICacheService
{
    Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
}

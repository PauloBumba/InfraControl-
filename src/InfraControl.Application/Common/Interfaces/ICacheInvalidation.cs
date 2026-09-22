namespace InfraControl.Application.Common.Interfaces;

public interface ICacheInvalidatingRequest
{
    IReadOnlyCollection<string> CacheKeysToInvalidate { get; }
}

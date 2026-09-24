namespace InfraControl.Application.Common.Interfaces;

public interface IInfrastructureProvider
{
    string Name { get; }
    bool CanHandle(string providerKey);
}

public interface IDiscoveryProvider : IInfrastructureProvider
{
    Task<DiscoveryProviderResult> DiscoverAsync(
        string target,
        CancellationToken cancellationToken = default);
}

public interface IMonitoringProvider : IInfrastructureProvider
{
    Task<MonitoringProviderResult> CheckAsync(
        string target,
        CancellationToken cancellationToken = default);
}

public sealed record DiscoveryProviderResult(
    bool Succeeded,
    IReadOnlyDictionary<string, string> Attributes,
    string? Error = null);

public sealed record MonitoringProviderResult(
    bool Succeeded,
    TimeSpan Duration,
    string? Error = null,
    IReadOnlyDictionary<string, string>? Values = null);

public interface IProviderRegistry<TProvider>
    where TProvider : IInfrastructureProvider
{
    TProvider Resolve(string providerKey);
    IReadOnlyCollection<TProvider> GetAll();
}

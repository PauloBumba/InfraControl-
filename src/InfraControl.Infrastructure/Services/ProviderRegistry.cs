using InfraControl.Application.Common.Interfaces;

namespace InfraControl.Infrastructure.Services;

public sealed class ProviderRegistry<TProvider>(IEnumerable<TProvider> providers)
    : IProviderRegistry<TProvider>
    where TProvider : IInfrastructureProvider
{
    private readonly IReadOnlyCollection<TProvider> _providers = providers.ToArray();

    public IReadOnlyCollection<TProvider> GetAll() => _providers;

    public TProvider Resolve(string providerKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
            throw new ArgumentException("Provider key is required.", nameof(providerKey));

        return _providers.FirstOrDefault(provider => provider.CanHandle(providerKey))
            ?? throw new InvalidOperationException($"No provider registered for '{providerKey}'.");
    }
}

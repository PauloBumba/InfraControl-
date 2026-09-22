using InfraControl.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace InfraControl.Infrastructure.Services;

public sealed class FeatureFlagProvider(
    IConfiguration configuration,
    ICacheService cache) : IFeatureFlagProvider
{
    private const string Prefix = "infracontrol:feature-flag:";

    public async Task<bool> IsEnabledAsync(
        string name,
        bool defaultValue = false,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        var overrideValue = await cache.GetAsync<bool?>($"{Prefix}{name}", cancellationToken);
        if (overrideValue.HasValue)
            return overrideValue.Value;

        return configuration.GetValue($"FeatureFlags:{name}", defaultValue);
    }

    public Task SetOverrideAsync(
        string name,
        bool enabled,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        return cache.SetAsync($"{Prefix}{name}", enabled, duration ?? TimeSpan.FromMinutes(30), cancellationToken);
    }

    public Task ClearOverrideAsync(string name, CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        return cache.RemoveAsync($"{Prefix}{name}", cancellationToken);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Feature flag name is required.", nameof(name));
    }
}

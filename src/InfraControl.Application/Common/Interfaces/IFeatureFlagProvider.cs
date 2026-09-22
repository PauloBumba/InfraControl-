namespace InfraControl.Application.Common.Interfaces;

public interface IFeatureFlagProvider
{
    Task<bool> IsEnabledAsync(string name, bool defaultValue = false, CancellationToken cancellationToken = default);
    Task SetOverrideAsync(string name, bool enabled, TimeSpan? duration = null, CancellationToken cancellationToken = default);
    Task ClearOverrideAsync(string name, CancellationToken cancellationToken = default);
}

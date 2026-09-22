namespace InfraControl.Application.Common.Interfaces;

public sealed record ResilienceOptions(
    int MaxRetries = 3,
    TimeSpan? Timeout = null,
    int FailureThreshold = 5,
    TimeSpan? BreakDuration = null)
{
    public TimeSpan EffectiveTimeout => Timeout ?? TimeSpan.FromSeconds(10);
    public TimeSpan EffectiveBreakDuration => BreakDuration ?? TimeSpan.FromSeconds(30);
}

public interface IResilienceExecutor
{
    Task<T> ExecuteAsync<T>(
        string policyName,
        Func<CancellationToken, Task<T>> operation,
        ResilienceOptions? options = null,
        CancellationToken cancellationToken = default);
}

public sealed class CircuitOpenException(string policyName)
    : InvalidOperationException($"The resilience circuit '{policyName}' is open.");

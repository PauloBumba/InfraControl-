using System.Collections.Concurrent;
using InfraControl.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Services;

public sealed class ResilienceExecutor(ILogger<ResilienceExecutor> logger) : IResilienceExecutor
{
    private readonly ConcurrentDictionary<string, Circuit> _circuits = new(StringComparer.OrdinalIgnoreCase);

    public async Task<T> ExecuteAsync<T>(
        string policyName,
        Func<CancellationToken, Task<T>> operation,
        ResilienceOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(policyName)) throw new ArgumentException("Policy name is required.", nameof(policyName));
        ArgumentNullException.ThrowIfNull(operation);

        var settings = options ?? new ResilienceOptions();
        var circuit = _circuits.GetOrAdd(policyName, _ => new Circuit());
        if (!circuit.TryEnter(settings.EffectiveBreakDuration))
            throw new CircuitOpenException(policyName);

        Exception? lastException = null;
        for (var attempt = 0; attempt <= Math.Max(0, settings.MaxRetries); attempt++)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(settings.EffectiveTimeout);
                var result = await operation(timeout.Token);
                circuit.RecordSuccess();
                return result;
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                lastException = exception;
                circuit.RecordFailure(settings.FailureThreshold);
                if (attempt >= settings.MaxRetries)
                    break;

                var delay = TimeSpan.FromMilliseconds(Math.Min(30_000, 250 * Math.Pow(2, attempt)));
                logger.LogWarning(exception, "Provider policy {PolicyName} failed on attempt {Attempt}; retrying in {Delay}.", policyName, attempt + 1, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException($"Provider policy '{policyName}' exhausted retries.", lastException);
    }

    private sealed class Circuit
    {
        private readonly object _sync = new();
        private int _failures;
        private DateTimeOffset? _openedAt;

        public bool TryEnter(TimeSpan breakDuration)
        {
            lock (_sync)
            {
                if (_openedAt is null) return true;
                if (DateTimeOffset.UtcNow - _openedAt.Value >= breakDuration)
                {
                    _openedAt = null;
                    _failures = 0;
                    return true;
                }

                return false;
            }
        }

        public void RecordSuccess()
        {
            lock (_sync)
            {
                _failures = 0;
                _openedAt = null;
            }
        }

        public void RecordFailure(int threshold)
        {
            lock (_sync)
            {
                _failures++;
                if (_failures >= Math.Max(1, threshold))
                    _openedAt ??= DateTimeOffset.UtcNow;
            }
        }
    }
}

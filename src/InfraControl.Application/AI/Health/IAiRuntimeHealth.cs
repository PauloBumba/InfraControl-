namespace InfraControl.Application.AI.Health;

public enum AiComponentStatus
{
    Healthy,
    Degraded
}

public sealed record AiComponentReport(
    string Component,
    AiComponentStatus Status,
    string? Detail = null);

public sealed record AiRuntimeHealthReport(
    bool IsHealthy,
    IReadOnlyCollection<AiComponentReport> Components);

public interface IAiRuntimeHealth
{
    Task<AiRuntimeHealthReport> CheckAsync(CancellationToken cancellationToken = default);
}

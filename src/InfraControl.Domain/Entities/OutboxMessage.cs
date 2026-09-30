using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

public class OutboxMessage : Entity
{
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;

    /// <summary>
    /// ADR-021: a requisição que originou o evento. Quem publica no broker é
    /// OUTRO processo, sem acesso ao LogContext — sem gravar isto aqui, o
    /// rastro morre entre a API e o worker, que é onde os problemas realmente aparecem.
    /// </summary>
    public string? CorrelationId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(
        string eventType,
        string payload,
        DateTime occurredAt,
        int maxRetries = 5,
        string? correlationId = null)
    {
        EventType = eventType;
        Payload = payload;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
        MaxRetries = maxRetries;
        RetryCount = 0;
    }

    public void MarkProcessed()
    {
        ProcessedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Error = error;
        RetryCount++;
    }

    public bool ShouldRetry => RetryCount < MaxRetries;
}

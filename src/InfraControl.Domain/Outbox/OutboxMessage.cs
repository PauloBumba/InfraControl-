using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Outbox;

public class OutboxMessage : Entity
{
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
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
        int maxRetries = 5)
    {
        EventType = eventType;
        Payload = payload;
        OccurredAt = occurredAt;
        MaxRetries = maxRetries;
        RetryCount = 0;
    }

    public void MarkProcessed()
    {
        ProcessedAt = DateTime.UtcNow;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Error = error;
        RetryCount++;
    }

    public bool ShouldRetry => RetryCount < MaxRetries;
}

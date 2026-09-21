using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Outbox;

public class ProcessedMessage : Entity
{
    public Guid EventId { get; private set; }
    public string Consumer { get; private set; } = string.Empty;
    public DateTime ProcessedAt { get; private set; }

    private ProcessedMessage() { }

    public ProcessedMessage(Guid eventId, string consumer)
    {
        EventId = eventId;
        Consumer = consumer;
        ProcessedAt = DateTime.UtcNow;
    }
}

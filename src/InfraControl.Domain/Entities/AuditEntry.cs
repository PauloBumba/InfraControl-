using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

public sealed class AuditEntry : Entity
{
    private AuditEntry() { }

    public AuditEntry(
        string action,
        string resource,
        string outcome,
        string? actorId = null,
        string? correlationId = null,
        string? error = null)
    {
        if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("Audit action is required.", nameof(action));
        if (string.IsNullOrWhiteSpace(resource)) throw new ArgumentException("Audit resource is required.", nameof(resource));
        if (string.IsNullOrWhiteSpace(outcome)) throw new ArgumentException("Audit outcome is required.", nameof(outcome));

        Action = action.Trim();
        Resource = resource.Trim();
        Outcome = outcome.Trim();
        ActorId = actorId;
        CorrelationId = correlationId;
        Error = error;
        OccurredAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
    }

    public string Action { get; private set; } = string.Empty;
    public string Resource { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public string? ActorId { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? Error { get; private set; }
    public DateTime OccurredAt { get; private set; }
}

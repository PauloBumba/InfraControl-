using InfraControl.Domain.Common.Events;
using InfraControl.Domain.Enums;
using InfraControl.Domain.RemoteAccess;

namespace InfraControl.Domain.Events;

// ADR-030 §5 — ciclo da sessão de acesso remoto (sem DELETE: histórico).

public sealed class RemoteSessionCreatedDomainEvent : DomainEvent
{
    public RemoteSessionCreatedDomainEvent(Guid remoteSessionId, Guid deviceId, Guid userId, RemoteProtocol protocol)
    {
        RemoteSessionId = remoteSessionId;
        DeviceId = deviceId;
        UserId = userId;
        Protocol = protocol;
    }

    public Guid RemoteSessionId { get; }
    public Guid DeviceId { get; }
    public Guid UserId { get; }
    public RemoteProtocol Protocol { get; }
    public override string EventType => nameof(RemoteSessionCreatedDomainEvent);
}

public sealed class RemoteSessionUpdatedDomainEvent : DomainEvent
{
    public RemoteSessionUpdatedDomainEvent(Guid remoteSessionId, Guid deviceId, RemoteSessionStatus status)
    {
        RemoteSessionId = remoteSessionId;
        DeviceId = deviceId;
        Status = status;
    }

    public Guid RemoteSessionId { get; }
    public Guid DeviceId { get; }
    public RemoteSessionStatus Status { get; }
    public override string EventType => nameof(RemoteSessionUpdatedDomainEvent);
}

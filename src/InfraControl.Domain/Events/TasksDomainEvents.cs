using InfraControl.Domain.Common.Events;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Events;

// ADR-030 §5 — eventos CRUD dos agregados operacionais de Tasks.

public sealed class TaskItemCreatedDomainEvent : DomainEvent
{
    public TaskItemCreatedDomainEvent(Guid taskId, string title, TaskKind kind)
    {
        TaskId = taskId;
        Title = title;
        Kind = kind;
    }

    public Guid TaskId { get; }
    public string Title { get; }
    public TaskKind Kind { get; }
    public override string EventType => nameof(TaskItemCreatedDomainEvent);
}

public sealed class TaskItemUpdatedDomainEvent : DomainEvent
{
    public TaskItemUpdatedDomainEvent(Guid taskId, string title, TaskState state)
    {
        TaskId = taskId;
        Title = title;
        State = state;
    }

    public Guid TaskId { get; }
    public string Title { get; }
    public TaskState State { get; }
    public override string EventType => nameof(TaskItemUpdatedDomainEvent);
}

public sealed class TaskItemDeletedDomainEvent : DomainEvent
{
    public TaskItemDeletedDomainEvent(Guid taskId, string title)
    {
        TaskId = taskId;
        Title = title;
    }

    public Guid TaskId { get; }
    public string Title { get; }
    public override string EventType => nameof(TaskItemDeletedDomainEvent);
}

public sealed class TaskCommentCreatedDomainEvent : DomainEvent
{
    public TaskCommentCreatedDomainEvent(Guid taskCommentId, Guid taskId, Guid authorId)
    {
        TaskCommentId = taskCommentId;
        TaskId = taskId;
        AuthorId = authorId;
    }

    public Guid TaskCommentId { get; }
    public Guid TaskId { get; }
    public Guid AuthorId { get; }
    public override string EventType => nameof(TaskCommentCreatedDomainEvent);
}

public sealed class TaskCommentUpdatedDomainEvent : DomainEvent
{
    public TaskCommentUpdatedDomainEvent(Guid taskCommentId, Guid taskId)
    {
        TaskCommentId = taskCommentId;
        TaskId = taskId;
    }

    public Guid TaskCommentId { get; }
    public Guid TaskId { get; }
    public override string EventType => nameof(TaskCommentUpdatedDomainEvent);
}

public sealed class TaskCommentDeletedDomainEvent : DomainEvent
{
    public TaskCommentDeletedDomainEvent(Guid taskCommentId, Guid taskId)
    {
        TaskCommentId = taskCommentId;
        TaskId = taskId;
    }

    public Guid TaskCommentId { get; }
    public Guid TaskId { get; }
    public override string EventType => nameof(TaskCommentDeletedDomainEvent);
}

public sealed class TaskAssigneeCreatedDomainEvent : DomainEvent
{
    public TaskAssigneeCreatedDomainEvent(Guid taskId, Guid userId)
    {
        TaskId = taskId;
        UserId = userId;
    }

    public Guid TaskId { get; }
    public Guid UserId { get; }
    public override string EventType => nameof(TaskAssigneeCreatedDomainEvent);
}

public sealed class TaskAssigneeDeletedDomainEvent : DomainEvent
{
    public TaskAssigneeDeletedDomainEvent(Guid taskId, Guid userId)
    {
        TaskId = taskId;
        UserId = userId;
    }

    public Guid TaskId { get; }
    public Guid UserId { get; }
    public override string EventType => nameof(TaskAssigneeDeletedDomainEvent);
}

public sealed class TaskDeviceCreatedDomainEvent : DomainEvent
{
    public TaskDeviceCreatedDomainEvent(Guid taskId, Guid deviceId)
    {
        TaskId = taskId;
        DeviceId = deviceId;
    }

    public Guid TaskId { get; }
    public Guid DeviceId { get; }
    public override string EventType => nameof(TaskDeviceCreatedDomainEvent);
}

public sealed class TaskDeviceDeletedDomainEvent : DomainEvent
{
    public TaskDeviceDeletedDomainEvent(Guid taskId, Guid deviceId)
    {
        TaskId = taskId;
        DeviceId = deviceId;
    }

    public Guid TaskId { get; }
    public Guid DeviceId { get; }
    public override string EventType => nameof(TaskDeviceDeletedDomainEvent);
}

public sealed class TaskIncidentCreatedDomainEvent : DomainEvent
{
    public TaskIncidentCreatedDomainEvent(Guid taskId, Guid incidentId)
    {
        TaskId = taskId;
        IncidentId = incidentId;
    }

    public Guid TaskId { get; }
    public Guid IncidentId { get; }
    public override string EventType => nameof(TaskIncidentCreatedDomainEvent);
}

public sealed class TaskIncidentDeletedDomainEvent : DomainEvent
{
    public TaskIncidentDeletedDomainEvent(Guid taskId, Guid incidentId)
    {
        TaskId = taskId;
        IncidentId = incidentId;
    }

    public Guid TaskId { get; }
    public Guid IncidentId { get; }
    public override string EventType => nameof(TaskIncidentDeletedDomainEvent);
}

public sealed class TaskSlaCreatedDomainEvent : DomainEvent
{
    public TaskSlaCreatedDomainEvent(Guid taskId, Guid slaPolicyId)
    {
        TaskId = taskId;
        SlaPolicyId = slaPolicyId;
    }

    public Guid TaskId { get; }
    public Guid SlaPolicyId { get; }
    public override string EventType => nameof(TaskSlaCreatedDomainEvent);
}

public sealed class TaskSlaUpdatedDomainEvent : DomainEvent
{
    public TaskSlaUpdatedDomainEvent(Guid taskId, DateTime responseDueAt, DateTime resolutionDueAt)
    {
        TaskId = taskId;
        ResponseDueAt = responseDueAt;
        ResolutionDueAt = resolutionDueAt;
    }

    public Guid TaskId { get; }
    public DateTime ResponseDueAt { get; }
    public DateTime ResolutionDueAt { get; }
    public override string EventType => nameof(TaskSlaUpdatedDomainEvent);
}

public sealed class TaskSlaPauseCreatedDomainEvent : DomainEvent
{
    public TaskSlaPauseCreatedDomainEvent(Guid taskSlaPauseId, Guid taskId)
    {
        TaskSlaPauseId = taskSlaPauseId;
        TaskId = taskId;
    }

    public Guid TaskSlaPauseId { get; }
    public Guid TaskId { get; }
    public override string EventType => nameof(TaskSlaPauseCreatedDomainEvent);
}

public sealed class TaskSlaPauseUpdatedDomainEvent : DomainEvent
{
    public TaskSlaPauseUpdatedDomainEvent(Guid taskSlaPauseId, Guid taskId, DateTime? endAt)
    {
        TaskSlaPauseId = taskSlaPauseId;
        TaskId = taskId;
        EndAt = endAt;
    }

    public Guid TaskSlaPauseId { get; }
    public Guid TaskId { get; }
    public DateTime? EndAt { get; }
    public override string EventType => nameof(TaskSlaPauseUpdatedDomainEvent);
}

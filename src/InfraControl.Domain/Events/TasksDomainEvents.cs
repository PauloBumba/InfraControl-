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

// F23-T003 — critérios de aceite (Plano de Execução §12): CRUD completo
// no mesmo molde dos comentários.

public sealed class TaskCriterionCreatedDomainEvent : DomainEvent
{
    public TaskCriterionCreatedDomainEvent(Guid taskCriterionId, Guid taskId)
    {
        TaskCriterionId = taskCriterionId;
        TaskId = taskId;
    }

    public Guid TaskCriterionId { get; }
    public Guid TaskId { get; }
    public override string EventType => nameof(TaskCriterionCreatedDomainEvent);
}

public sealed class TaskCriterionUpdatedDomainEvent : DomainEvent
{
    public TaskCriterionUpdatedDomainEvent(Guid taskCriterionId, Guid taskId)
    {
        TaskCriterionId = taskCriterionId;
        TaskId = taskId;
    }

    public Guid TaskCriterionId { get; }
    public Guid TaskId { get; }
    public override string EventType => nameof(TaskCriterionUpdatedDomainEvent);
}

public sealed class TaskCriterionDeletedDomainEvent : DomainEvent
{
    public TaskCriterionDeletedDomainEvent(Guid taskCriterionId, Guid taskId)
    {
        TaskCriterionId = taskCriterionId;
        TaskId = taskId;
    }

    public Guid TaskCriterionId { get; }
    public Guid TaskId { get; }
    public override string EventType => nameof(TaskCriterionDeletedDomainEvent);
}

// F23-T004/P4b — impedimentos estruturados (Plano de Execução §10): CRUD
// completo no mesmo molde dos critérios.

public sealed class TaskImpedimentCreatedDomainEvent : DomainEvent
{
    public TaskImpedimentCreatedDomainEvent(Guid taskImpedimentId, Guid taskId)
    {
        TaskImpedimentId = taskImpedimentId;
        TaskId = taskId;
    }

    public Guid TaskImpedimentId { get; }
    public Guid TaskId { get; }
    public override string EventType => nameof(TaskImpedimentCreatedDomainEvent);
}

public sealed class TaskImpedimentUpdatedDomainEvent : DomainEvent
{
    public TaskImpedimentUpdatedDomainEvent(Guid taskImpedimentId, Guid taskId)
    {
        TaskImpedimentId = taskImpedimentId;
        TaskId = taskId;
    }

    public Guid TaskImpedimentId { get; }
    public Guid TaskId { get; }
    public override string EventType => nameof(TaskImpedimentUpdatedDomainEvent);
}

public sealed class TaskImpedimentDeletedDomainEvent : DomainEvent
{
    public TaskImpedimentDeletedDomainEvent(Guid taskImpedimentId, Guid taskId)
    {
        TaskImpedimentId = taskImpedimentId;
        TaskId = taskId;
    }

    public Guid TaskImpedimentId { get; }
    public Guid TaskId { get; }
    public override string EventType => nameof(TaskImpedimentDeletedDomainEvent);
}

// F23-T004/P4c — evidências anexadas à task (Plano de Execução §11):
// upload em disco local (ADR-033); CRUD sem edição (Add/Delete).

public sealed class TaskEvidenceCreatedDomainEvent : DomainEvent
{
    public TaskEvidenceCreatedDomainEvent(Guid taskEvidenceId, Guid taskId)
    {
        TaskEvidenceId = taskEvidenceId;
        TaskId = taskId;
    }

    public Guid TaskEvidenceId { get; }
    public Guid TaskId { get; }
    public override string EventType => nameof(TaskEvidenceCreatedDomainEvent);
}

public sealed class TaskEvidenceDeletedDomainEvent : DomainEvent
{
    public TaskEvidenceDeletedDomainEvent(Guid taskEvidenceId, Guid taskId)
    {
        TaskEvidenceId = taskEvidenceId;
        TaskId = taskId;
    }

    public Guid TaskEvidenceId { get; }
    public Guid TaskId { get; }
    public override string EventType => nameof(TaskEvidenceDeletedDomainEvent);
}

// F23-T004/P4d — fases dentro da task (Plano de Execução §7): CRUD
// completo; "concluir/reabrir" e "atual" são variações de Update.

public sealed class TaskPhaseCreatedDomainEvent : DomainEvent
{
    public TaskPhaseCreatedDomainEvent(Guid taskPhaseId, Guid taskId)
    {
        TaskPhaseId = taskPhaseId;
        TaskId = taskId;
    }

    public Guid TaskPhaseId { get; }
    public Guid TaskId { get; }
    public override string EventType => nameof(TaskPhaseCreatedDomainEvent);
}

public sealed class TaskPhaseUpdatedDomainEvent : DomainEvent
{
    public TaskPhaseUpdatedDomainEvent(Guid taskPhaseId, Guid taskId)
    {
        TaskPhaseId = taskPhaseId;
        TaskId = taskId;
    }

    public Guid TaskPhaseId { get; }
    public Guid TaskId { get; }
    public override string EventType => nameof(TaskPhaseUpdatedDomainEvent);
}

public sealed class TaskPhaseDeletedDomainEvent : DomainEvent
{
    public TaskPhaseDeletedDomainEvent(Guid taskPhaseId, Guid taskId)
    {
        TaskPhaseId = taskPhaseId;
        TaskId = taskId;
    }

    public Guid TaskPhaseId { get; }
    public Guid TaskId { get; }
    public override string EventType => nameof(TaskPhaseDeletedDomainEvent);
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

public sealed class TaskKnowledgeArticleCreatedDomainEvent : DomainEvent
{
    public TaskKnowledgeArticleCreatedDomainEvent(Guid taskId, Guid articleId)
    {
        TaskId = taskId;
        ArticleId = articleId;
    }

    public Guid TaskId { get; }
    public Guid ArticleId { get; }
    public override string EventType => nameof(TaskKnowledgeArticleCreatedDomainEvent);
}

public sealed class TaskKnowledgeArticleDeletedDomainEvent : DomainEvent
{
    public TaskKnowledgeArticleDeletedDomainEvent(Guid taskId, Guid articleId)
    {
        TaskId = taskId;
        ArticleId = articleId;
    }

    public Guid TaskId { get; }
    public Guid ArticleId { get; }
    public override string EventType => nameof(TaskKnowledgeArticleDeletedDomainEvent);
}

public sealed class TaskPdiCreatedDomainEvent : DomainEvent
{
    public TaskPdiCreatedDomainEvent(Guid taskId, Guid userId)
    {
        TaskId = taskId;
        UserId = userId;
    }

    public Guid TaskId { get; }
    public Guid UserId { get; }
    public override string EventType => nameof(TaskPdiCreatedDomainEvent);
}

public sealed class TaskPdiDeletedDomainEvent : DomainEvent
{
    public TaskPdiDeletedDomainEvent(Guid taskId, Guid userId)
    {
        TaskId = taskId;
        UserId = userId;
    }

    public Guid TaskId { get; }
    public Guid UserId { get; }
    public override string EventType => nameof(TaskPdiDeletedDomainEvent);
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

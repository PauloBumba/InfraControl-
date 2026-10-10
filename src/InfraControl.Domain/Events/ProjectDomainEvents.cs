using InfraControl.Domain.Common.Events;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Events;

/// <summary>
/// Eventos de domínio do ciclo de vida e governança de Projetos (ADR-043).
/// </summary>
public sealed class ProjectCreatedDomainEvent : DomainEvent
{
    public ProjectCreatedDomainEvent(Guid projectId, string code, string title, Guid leaderId)
    {
        ProjectId = projectId;
        Code = code;
        Title = title;
        LeaderId = leaderId;
    }

    public Guid ProjectId { get; }
    public string Code { get; }
    public string Title { get; }
    public Guid LeaderId { get; }
    public override string EventType => nameof(ProjectCreatedDomainEvent);
}

public sealed class ProjectUpdatedDomainEvent : DomainEvent
{
    public ProjectUpdatedDomainEvent(Guid projectId, string title, ProjectStatus status)
    {
        ProjectId = projectId;
        Title = title;
        Status = status;
    }

    public Guid ProjectId { get; }
    public string Title { get; }
    public ProjectStatus Status { get; }
    public override string EventType => nameof(ProjectUpdatedDomainEvent);
}

public sealed class ProjectStatusChangedDomainEvent : DomainEvent
{
    public ProjectStatusChangedDomainEvent(Guid projectId, ProjectStatus oldStatus, ProjectStatus newStatus, string? reason)
    {
        ProjectId = projectId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        Reason = reason;
    }

    public Guid ProjectId { get; }
    public ProjectStatus OldStatus { get; }
    public ProjectStatus NewStatus { get; }
    public string? Reason { get; }
    public override string EventType => nameof(ProjectStatusChangedDomainEvent);
}

public sealed class ProjectApprovedDomainEvent : DomainEvent
{
    public ProjectApprovedDomainEvent(Guid projectId, Guid adminApproverId, string? approvalNotes, DateTimeOffset approvedAt)
    {
        ProjectId = projectId;
        AdminApproverId = adminApproverId;
        ApprovalNotes = approvalNotes;
        ApprovedAt = approvedAt;
    }

    public Guid ProjectId { get; }
    public Guid AdminApproverId { get; }
    public string? ApprovalNotes { get; }
    public DateTimeOffset ApprovedAt { get; }
    public override string EventType => nameof(ProjectApprovedDomainEvent);
}

public sealed class ProjectDeletedDomainEvent : DomainEvent
{
    public ProjectDeletedDomainEvent(Guid projectId, string title)
    {
        ProjectId = projectId;
        Title = title;
    }

    public Guid ProjectId { get; }
    public string Title { get; }
    public override string EventType => nameof(ProjectDeletedDomainEvent);
}

public sealed class ProjectMemberAddedDomainEvent : DomainEvent
{
    public ProjectMemberAddedDomainEvent(Guid projectId, Guid userId, ProjectMemberRole role)
    {
        ProjectId = projectId;
        UserId = userId;
        Role = role;
    }

    public Guid ProjectId { get; }
    public Guid UserId { get; }
    public ProjectMemberRole Role { get; }
    public override string EventType => nameof(ProjectMemberAddedDomainEvent);
}

public sealed class ProjectMemberRemovedDomainEvent : DomainEvent
{
    public ProjectMemberRemovedDomainEvent(Guid projectId, Guid userId)
    {
        ProjectId = projectId;
        UserId = userId;
    }

    public Guid ProjectId { get; }
    public Guid UserId { get; }
    public override string EventType => nameof(ProjectMemberRemovedDomainEvent);
}

public sealed class ProjectPhaseCreatedDomainEvent : DomainEvent
{
    public ProjectPhaseCreatedDomainEvent(Guid phaseId, Guid projectId, string title, int order)
    {
        PhaseId = phaseId;
        ProjectId = projectId;
        Title = title;
        Order = order;
    }

    public Guid PhaseId { get; }
    public Guid ProjectId { get; }
    public string Title { get; }
    public int Order { get; }
    public override string EventType => nameof(ProjectPhaseCreatedDomainEvent);
}

public sealed class ProjectPhaseUpdatedDomainEvent : DomainEvent
{
    public ProjectPhaseUpdatedDomainEvent(Guid phaseId, Guid projectId, string title)
    {
        PhaseId = phaseId;
        ProjectId = projectId;
        Title = title;
    }

    public Guid PhaseId { get; }
    public Guid ProjectId { get; }
    public string Title { get; }
    public override string EventType => nameof(ProjectPhaseUpdatedDomainEvent);
}

public sealed class ProjectPhaseStatusChangedDomainEvent : DomainEvent
{
    public ProjectPhaseStatusChangedDomainEvent(Guid phaseId, Guid projectId, ProjectPhaseStatus oldStatus, ProjectPhaseStatus newStatus)
    {
        PhaseId = phaseId;
        ProjectId = projectId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }

    public Guid PhaseId { get; }
    public Guid ProjectId { get; }
    public ProjectPhaseStatus OldStatus { get; }
    public ProjectPhaseStatus NewStatus { get; }
    public override string EventType => nameof(ProjectPhaseStatusChangedDomainEvent);
}

public sealed class ProjectPhaseDeletedDomainEvent : DomainEvent
{
    public ProjectPhaseDeletedDomainEvent(Guid phaseId, Guid projectId, string title)
    {
        PhaseId = phaseId;
        ProjectId = projectId;
        Title = title;
    }

    public Guid PhaseId { get; }
    public Guid ProjectId { get; }
    public string Title { get; }
    public override string EventType => nameof(ProjectPhaseDeletedDomainEvent);
}

using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Agregado raiz de Projetos de Infraestrutura e TI (ADR-043 · docs/38 §3.1).
/// </summary>
public sealed class Project : AggregateRoot
{
    public const int CodeMaxLength = 20;
    public const int TitleMaxLength = 150;
    public const int DescriptionMaxLength = 4000;
    public const int JustificationMaxLength = 2000;
    public const int ApprovalNotesMaxLength = 2000;

    public string Code { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? Justification { get; private set; }
    public ProjectPriority Priority { get; private set; }
    public ProjectStatus Status { get; private set; }

    /// <summary>Líder técnico responsável pela condução do projeto.</summary>
    public Guid LeaderId { get; private set; }

    /// <summary>Usuário autenticado que criou o projeto; nulo apenas em registros legados.</summary>
    public Guid? CreatedById { get; private set; }

    /// <summary>Patrocinador ou gestor da área.</summary>
    public Guid? ManagerId { get; private set; }

    /// <summary>Administrador que homologou o encerramento do projeto.</summary>
    public Guid? AdminApproverId { get; private set; }

    /// <summary>Parecer formal do administrador na homologação.</summary>
    public string? ApprovalNotes { get; private set; }

    /// <summary>Data e hora UTC em que o projeto foi homologado e aprovado.</summary>
    public DateTimeOffset? ApprovedAt { get; private set; }

    public DateOnly? PlannedStartDate { get; private set; }
    public DateOnly? PlannedEndDate { get; private set; }
    public DateOnly? ActualStartDate { get; private set; }
    public DateOnly? ActualEndDate { get; private set; }

    private readonly List<ProjectMember> _members = [];
    public IReadOnlyCollection<ProjectMember> Members => _members.AsReadOnly();

    private Project() { }

    public static Project Create(
        string code,
        string title,
        string description,
        Guid leaderId,
        ProjectPriority priority = ProjectPriority.Medium,
        string? justification = null,
        Guid? managerId = null,
        DateOnly? plannedStartDate = null,
        DateOnly? plannedEndDate = null,
        Guid? createdById = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("O código do projeto é obrigatório.", nameof(code));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título do projeto é obrigatório.", nameof(title));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("A descrição do projeto é obrigatória.", nameof(description));

        if (leaderId == Guid.Empty)
            throw new ArgumentException("O líder técnico do projeto é obrigatório.", nameof(leaderId));

        if (createdById == Guid.Empty)
            throw new ArgumentException("O criador do projeto não pode ser vazio.", nameof(createdById));

        code = code.Trim().ToUpperInvariant();
        if (code.Length > CodeMaxLength)
            throw new ArgumentException($"O código do projeto pode ter no máximo {CodeMaxLength} caracteres.", nameof(code));

        title = title.Trim();
        if (title.Length > TitleMaxLength)
            throw new ArgumentException($"O título do projeto pode ter no máximo {TitleMaxLength} caracteres.", nameof(title));

        description = description.Trim();
        if (description.Length > DescriptionMaxLength)
            throw new ArgumentException($"A descrição do projeto pode ter no máximo {DescriptionMaxLength} caracteres.", nameof(description));

        if (justification?.Length > JustificationMaxLength)
            throw new ArgumentException($"A justificativa do projeto pode ter no máximo {JustificationMaxLength} caracteres.", nameof(justification));

        if (plannedStartDate.HasValue && plannedEndDate.HasValue && plannedEndDate.Value < plannedStartDate.Value)
            throw new ArgumentException("A data de término planejada não pode ser anterior à data de início planejada.");

        var project = new Project
        {
            Code = code,
            Title = title,
            Description = description,
            LeaderId = leaderId,
            CreatedById = createdById,
            Priority = priority,
            Status = ProjectStatus.Draft,
            Justification = string.IsNullOrWhiteSpace(justification) ? null : justification.Trim(),
            ManagerId = managerId,
            PlannedStartDate = plannedStartDate,
            PlannedEndDate = plannedEndDate,
        };

        project.RaiseDomainEvent(new ProjectCreatedDomainEvent(project.Id, project.Code, project.Title, project.LeaderId));
        return project;
    }

    public void UpdateDetails(
        string title,
        string description,
        string? justification,
        ProjectPriority priority,
        Guid leaderId,
        Guid? managerId)
    {
        if (Status is ProjectStatus.Completed or ProjectStatus.Cancelled)
            throw new InvalidOperationException($"Não é possível alterar dados de um projeto no estado '{Status}'.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título do projeto é obrigatório.", nameof(title));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("A descrição do projeto é obrigatória.", nameof(description));

        if (leaderId == Guid.Empty)
            throw new ArgumentException("O líder técnico do projeto é obrigatório.", nameof(leaderId));

        title = title.Trim();
        if (title.Length > TitleMaxLength)
            throw new ArgumentException($"O título do projeto pode ter no máximo {TitleMaxLength} caracteres.", nameof(title));

        description = description.Trim();
        if (description.Length > DescriptionMaxLength)
            throw new ArgumentException($"A descrição do projeto pode ter no máximo {DescriptionMaxLength} caracteres.", nameof(description));

        if (justification?.Length > JustificationMaxLength)
            throw new ArgumentException($"A justificativa do projeto pode ter no máximo {JustificationMaxLength} caracteres.", nameof(justification));

        Title = title;
        Description = description;
        Justification = string.IsNullOrWhiteSpace(justification) ? null : justification.Trim();
        Priority = priority;
        LeaderId = leaderId;
        ManagerId = managerId;

        Touch();
        RaiseDomainEvent(new ProjectUpdatedDomainEvent(Id, Title, Status));
    }

    public void SetPlanningDates(DateOnly? plannedStart, DateOnly? plannedEnd)
    {
        if (plannedStart.HasValue && plannedEnd.HasValue && plannedEnd.Value < plannedStart.Value)
            throw new ArgumentException("A data final planejada não pode ser anterior à data inicial.");

        PlannedStartDate = plannedStart;
        PlannedEndDate = plannedEnd;
        Touch();
    }

    public void SetActualDates(DateOnly? actualStart, DateOnly? actualEnd)
    {
        if (actualStart.HasValue && actualEnd.HasValue && actualEnd.Value < actualStart.Value)
            throw new ArgumentException("A data final real não pode ser anterior à data inicial.");

        ActualStartDate = actualStart;
        ActualEndDate = actualEnd;
        Touch();
    }

    public void TransitionTo(ProjectStatus newStatus, string? reason = null)
    {
        if (newStatus == Status)
            return;

        if (!CanTransitionTo(newStatus))
            throw new InvalidOperationException($"Transição inválida do estado '{Status}' para '{newStatus}'.");

        var oldStatus = Status;
        Status = newStatus;

        if (newStatus == ProjectStatus.InProgress && ActualStartDate == null)
        {
            ActualStartDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        Touch();
        RaiseDomainEvent(new ProjectStatusChangedDomainEvent(Id, oldStatus, newStatus, reason));
    }

    public bool CanTransitionTo(ProjectStatus target)
    {
        return (Status, target) switch
        {
            (ProjectStatus.Draft, ProjectStatus.Planned) => true,
            (ProjectStatus.Draft, ProjectStatus.Cancelled) => true,

            (ProjectStatus.Planned, ProjectStatus.InProgress) => true,
            (ProjectStatus.Planned, ProjectStatus.Draft) => true,
            (ProjectStatus.Planned, ProjectStatus.Cancelled) => true,

            (ProjectStatus.InProgress, ProjectStatus.PendingFinalApproval) => true,
            (ProjectStatus.InProgress, ProjectStatus.OnHold) => true,
            (ProjectStatus.InProgress, ProjectStatus.Cancelled) => true,

            (ProjectStatus.OnHold, ProjectStatus.InProgress) => true,
            (ProjectStatus.OnHold, ProjectStatus.Cancelled) => true,

            (ProjectStatus.PendingFinalApproval, ProjectStatus.Completed) => true,
            (ProjectStatus.PendingFinalApproval, ProjectStatus.InProgress) => true,
            (ProjectStatus.PendingFinalApproval, ProjectStatus.Cancelled) => true,

            _ => false
        };
    }

    public void SubmitForApproval()
    {
        if (Status != ProjectStatus.InProgress)
            throw new InvalidOperationException("Apenas projetos em andamento podem ser submetidos para aprovação final.");

        TransitionTo(ProjectStatus.PendingFinalApproval);
    }

    public void Approve(Guid adminUserId, string? notes, DateTimeOffset approvedAt)
    {
        if (adminUserId == Guid.Empty)
            throw new ArgumentException("O identificador do administrador aprovador é obrigatório.", nameof(adminUserId));

        if (Status is not (ProjectStatus.PendingFinalApproval or ProjectStatus.InProgress))
            throw new InvalidOperationException($"Não é possível aprovar um projeto no estado '{Status}'.");

        if (notes?.Length > ApprovalNotesMaxLength)
            throw new ArgumentException($"O parecer de aprovação pode ter no máximo {ApprovalNotesMaxLength} caracteres.", nameof(notes));

        Status = ProjectStatus.Completed;
        AdminApproverId = adminUserId;
        ApprovalNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ApprovedAt = approvedAt;

        if (ActualEndDate == null)
        {
            ActualEndDate = DateOnly.FromDateTime(approvedAt.UtcDateTime);
        }

        Touch();
        RaiseDomainEvent(new ProjectApprovedDomainEvent(Id, adminUserId, ApprovalNotes, approvedAt));
    }

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("O motivo do cancelamento é obrigatório.", nameof(reason));

        if (Status is ProjectStatus.Completed or ProjectStatus.Cancelled)
            throw new InvalidOperationException($"Não é possível cancelar um projeto já no estado '{Status}'.");

        TransitionTo(ProjectStatus.Cancelled, reason.Trim());
    }

    public void PutOnHold(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("O motivo da pausa é obrigatório.", nameof(reason));

        if (Status != ProjectStatus.InProgress)
            throw new InvalidOperationException("Apenas projetos em andamento podem ser pausados.");

        TransitionTo(ProjectStatus.OnHold, reason.Trim());
    }

    public void Resume()
    {
        if (Status != ProjectStatus.OnHold)
            throw new InvalidOperationException("Apenas projetos pausados podem ser retomados.");

        TransitionTo(ProjectStatus.InProgress);
    }

    public void AddMember(Guid userId, ProjectMemberRole role)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("O ID do usuário é obrigatório.", nameof(userId));

        var existing = _members.FirstOrDefault(m => m.UserId == userId);
        if (existing is not null)
        {
            existing.UpdateRole(role);
        }
        else
        {
            var member = ProjectMember.Create(Id, userId, role);
            _members.Add(member);
            RaiseDomainEvent(new ProjectMemberAddedDomainEvent(Id, userId, role));
        }

        Touch();
    }

    public void RemoveMember(Guid userId)
    {
        var existing = _members.FirstOrDefault(m => m.UserId == userId);
        if (existing is not null)
        {
            _members.Remove(existing);
            Touch();
            RaiseDomainEvent(new ProjectMemberRemovedDomainEvent(Id, userId));
        }
    }

    public void Delete()
    {
        RaiseDomainEvent(new ProjectDeletedDomainEvent(Id, Title));
    }
}

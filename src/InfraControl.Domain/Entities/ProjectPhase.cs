using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Fase da esteira do Projeto (ADR-043 · docs/38 §3.2).
/// </summary>
public sealed class ProjectPhase : AggregateRoot
{
    public const int TitleMaxLength = 150;
    public const int DescriptionMaxLength = 2000;
    public const int CriteriaMaxLength = 1000;

    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int Order { get; private set; }
    public Guid? ResponsibleId { get; private set; }
    public DateOnly? PlannedStartDate { get; private set; }
    public DateOnly? PlannedEndDate { get; private set; }
    public DateOnly? ActualStartDate { get; private set; }
    public DateOnly? ActualEndDate { get; private set; }
    public string? CompletionCriteria { get; private set; }
    public ProjectPhaseStatus Status { get; private set; }
    public int Percent { get; private set; }

    private ProjectPhase() { }

    public static ProjectPhase Create(
        Guid projectId,
        string title,
        int order,
        string? description = null,
        string? completionCriteria = null,
        Guid? responsibleId = null,
        DateOnly? plannedStartDate = null,
        DateOnly? plannedEndDate = null)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("O ID do projeto é obrigatório.", nameof(projectId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título da fase é obrigatório.", nameof(title));

        title = title.Trim();
        if (title.Length > TitleMaxLength)
            throw new ArgumentException($"O título da fase pode ter no máximo {TitleMaxLength} caracteres.", nameof(title));

        if (description?.Length > DescriptionMaxLength)
            throw new ArgumentException($"A descrição da fase pode ter no máximo {DescriptionMaxLength} caracteres.", nameof(description));

        if (completionCriteria?.Length > CriteriaMaxLength)
            throw new ArgumentException($"Os critérios de aceite podem ter no máximo {CriteriaMaxLength} caracteres.", nameof(completionCriteria));

        if (plannedStartDate.HasValue && plannedEndDate.HasValue && plannedEndDate.Value < plannedStartDate.Value)
            throw new ArgumentException("A data final planejada não pode ser anterior à data inicial.");

        var phase = new ProjectPhase
        {
            ProjectId = projectId,
            Title = title,
            Order = order,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CompletionCriteria = string.IsNullOrWhiteSpace(completionCriteria) ? null : completionCriteria.Trim(),
            ResponsibleId = responsibleId,
            PlannedStartDate = plannedStartDate,
            PlannedEndDate = plannedEndDate,
            Status = ProjectPhaseStatus.NotStarted,
            Percent = 0,
        };

        phase.RaiseDomainEvent(new ProjectPhaseCreatedDomainEvent(phase.Id, phase.ProjectId, phase.Title, phase.Order));
        return phase;
    }

    public void Update(
        string title,
        string? description,
        string? completionCriteria,
        Guid? responsibleId,
        DateOnly? plannedStartDate,
        DateOnly? plannedEndDate)
    {
        if (Status == ProjectPhaseStatus.Completed)
            throw new InvalidOperationException("Não é possível alterar dados de uma fase já concluída.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título da fase é obrigatório.", nameof(title));

        title = title.Trim();
        if (title.Length > TitleMaxLength)
            throw new ArgumentException($"O título da fase pode ter no máximo {TitleMaxLength} caracteres.", nameof(title));

        if (description?.Length > DescriptionMaxLength)
            throw new ArgumentException($"A descrição da fase pode ter no máximo {DescriptionMaxLength} caracteres.", nameof(description));

        if (completionCriteria?.Length > CriteriaMaxLength)
            throw new ArgumentException($"Os critérios de aceite podem ter no máximo {CriteriaMaxLength} caracteres.", nameof(completionCriteria));

        if (plannedStartDate.HasValue && plannedEndDate.HasValue && plannedEndDate.Value < plannedStartDate.Value)
            throw new ArgumentException("A data final planejada não pode ser anterior à data inicial.");

        Title = title;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CompletionCriteria = string.IsNullOrWhiteSpace(completionCriteria) ? null : completionCriteria.Trim();
        ResponsibleId = responsibleId;
        PlannedStartDate = plannedStartDate;
        PlannedEndDate = plannedEndDate;

        Touch();
        RaiseDomainEvent(new ProjectPhaseUpdatedDomainEvent(Id, ProjectId, Title));
    }

    public void SetDates(DateOnly? actualStartDate, DateOnly? actualEndDate)
    {
        if (actualStartDate.HasValue && actualEndDate.HasValue && actualEndDate.Value < actualStartDate.Value)
            throw new ArgumentException("A data final real não pode ser anterior à data inicial.");

        ActualStartDate = actualStartDate;
        ActualEndDate = actualEndDate;
        Touch();
    }

    public void Start()
    {
        if (Status != ProjectPhaseStatus.NotStarted)
            throw new InvalidOperationException("A fase precisa estar no estado 'Não Iniciada' para ser iniciada.");

        var oldStatus = Status;
        Status = ProjectPhaseStatus.InProgress;

        if (ActualStartDate == null)
        {
            ActualStartDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        Touch();
        RaiseDomainEvent(new ProjectPhaseStatusChangedDomainEvent(Id, ProjectId, oldStatus, Status));
    }

    public void Submit()
    {
        if (Status != ProjectPhaseStatus.InProgress)
            throw new InvalidOperationException("Apenas fases em andamento podem ser submetidas para validação.");

        var oldStatus = Status;
        Status = ProjectPhaseStatus.PendingValidation;
        Percent = 100;

        Touch();
        RaiseDomainEvent(new ProjectPhaseStatusChangedDomainEvent(Id, ProjectId, oldStatus, Status));
    }

    public void Validate()
    {
        if (Status != ProjectPhaseStatus.PendingValidation)
            throw new InvalidOperationException("Apenas fases aguardando validação podem ser homologadas como concluídas.");

        var oldStatus = Status;
        Status = ProjectPhaseStatus.Completed;
        Percent = 100;

        if (ActualEndDate == null)
        {
            ActualEndDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        Touch();
        RaiseDomainEvent(new ProjectPhaseStatusChangedDomainEvent(Id, ProjectId, oldStatus, Status));
    }

    public void Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("O motivo da rejeição da fase é obrigatório.", nameof(reason));

        if (Status != ProjectPhaseStatus.PendingValidation)
            throw new InvalidOperationException("Apenas fases em validação podem ser rejeitadas para ajuste.");

        var oldStatus = Status;
        Status = ProjectPhaseStatus.InProgress;

        Touch();
        RaiseDomainEvent(new ProjectPhaseStatusChangedDomainEvent(Id, ProjectId, oldStatus, Status));
    }

    public void SetProgress(int percent)
    {
        if (percent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(percent), "O percentual de conclusão deve estar entre 0 e 100.");

        if (Status == ProjectPhaseStatus.Completed)
            throw new InvalidOperationException("Não é possível alterar o progresso de uma fase concluída.");

        Percent = percent;

        if (Status == ProjectPhaseStatus.NotStarted && percent > 0)
        {
            Start();
        }

        Touch();
    }

    public void SetOrder(int order)
    {
        if (order < 0)
            throw new ArgumentOutOfRangeException(nameof(order), "A ordenação da fase não pode ser negativa.");

        Order = order;
        Touch();
    }

    public void Delete()
    {
        RaiseDomainEvent(new ProjectPhaseDeletedDomainEvent(Id, ProjectId, Title));
    }
}

namespace InfraControl.Api.ViewModels.Projects;

public sealed class CreateProjectPhaseViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CompletionCriteria { get; set; }
    public Guid? ResponsibleId { get; set; }
    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
}

public sealed class UpdateProjectPhaseViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CompletionCriteria { get; set; }
    public Guid? ResponsibleId { get; set; }
    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
    public int? Percent { get; set; }
}

public sealed class ReorderProjectPhasesViewModel
{
    public Guid[] PhaseIdsInOrder { get; set; } = [];
}

public sealed class SetProjectPhaseStatusViewModel
{
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

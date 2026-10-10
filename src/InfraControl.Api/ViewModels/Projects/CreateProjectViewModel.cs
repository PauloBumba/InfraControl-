namespace InfraControl.Api.ViewModels.Projects;

public sealed class CreateProjectViewModel
{
    public string? Code { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Justification { get; set; }
    public string? Priority { get; set; }
    public Guid LeaderId { get; set; }
    public Guid? ManagerId { get; set; }
    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
}

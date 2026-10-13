namespace InfraControl.Api.ViewModels.Projects;

public sealed class CreateProjectImpedimentViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Technical";
    public string Severity { get; set; } = "Medium";
    public string? MitigationPlan { get; set; }
}

public sealed class UpdateProjectImpedimentViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Technical";
    public string Severity { get; set; } = "Medium";
    public string? MitigationPlan { get; set; }
}

public sealed class AddProjectMemberViewModel
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Member";
}

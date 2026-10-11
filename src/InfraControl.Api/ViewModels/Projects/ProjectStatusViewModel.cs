namespace InfraControl.Api.ViewModels.Projects;

public sealed class ProjectStatusViewModel
{
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

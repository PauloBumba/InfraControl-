namespace InfraControl.Application.Projects;

/// <summary>
/// DTO de leitura do agregado Project (ADR-043 · docs/38 §3.1).
/// </summary>
public sealed record ProjectDto(
    Guid Id,
    string Code,
    string Title,
    string Description,
    string? Justification,
    string Priority,
    string Status,
    Guid LeaderId,
    Guid? ManagerId,
    Guid? AdminApproverId,
    string? ApprovalNotes,
    DateTimeOffset? ApprovedAt,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    DateOnly? ActualStartDate,
    DateOnly? ActualEndDate)
{
    public static ProjectDto From(InfraControl.Domain.Entities.Project project) => new(
        project.Id,
        project.Code,
        project.Title,
        project.Description,
        project.Justification,
        project.Priority.ToString(),
        project.Status.ToString(),
        project.LeaderId,
        project.ManagerId,
        project.AdminApproverId,
        project.ApprovalNotes,
        project.ApprovedAt,
        project.PlannedStartDate,
        project.PlannedEndDate,
        project.ActualStartDate,
        project.ActualEndDate);
}

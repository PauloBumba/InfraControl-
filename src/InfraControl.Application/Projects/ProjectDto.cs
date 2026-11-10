namespace InfraControl.Application.Projects;

public sealed record ProjectMemberDto(
    Guid UserId,
    string Role,
    string? DisplayName = null)
{
    public static ProjectMemberDto From(
        InfraControl.Domain.Entities.ProjectMember member,
        IReadOnlyDictionary<Guid, string>? names = null) =>
        new(
            member.UserId,
            member.Role.ToString(),
            names is not null && names.TryGetValue(member.UserId, out var name)
                ? name
                : null);
}

/// <summary>
/// DTO de leitura do agregado Project (ADR-043 · docs/38 §3.1).
/// Os campos *Name resolvem o Username/DisplayName para apresentação
/// (spec §4) — os Ids continuam sendo as chaves de relacionamento.
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
    string? LeaderName,
    Guid? ManagerId,
    string? ManagerName,
    Guid? AdminApproverId,
    string? AdminApproverName,
    string? ApprovalNotes,
    DateTimeOffset? ApprovedAt,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    DateOnly? ActualStartDate,
    DateOnly? ActualEndDate,
    IReadOnlyCollection<ProjectMemberDto> Members,
    Guid? CreatedById,
    string? CreatedByName)
{
    public static ProjectDto From(
        InfraControl.Domain.Entities.Project project,
        IReadOnlyDictionary<Guid, string>? names = null) => new(
        project.Id,
        project.Code,
        project.Title,
        project.Description,
        project.Justification,
        project.Priority.ToString(),
        project.Status.ToString(),
        project.LeaderId,
        Resolve(names, project.LeaderId),
        project.ManagerId,
        Resolve(names, project.ManagerId),
        project.AdminApproverId,
        Resolve(names, project.AdminApproverId),
        project.ApprovalNotes,
        project.ApprovedAt,
        project.PlannedStartDate,
        project.PlannedEndDate,
        project.ActualStartDate,
        project.ActualEndDate,
        project.Members
            .Select(m => ProjectMemberDto.From(m, names))
            .ToArray(),
        project.CreatedById,
        Resolve(names, project.CreatedById));

    private static string? Resolve(
        IReadOnlyDictionary<Guid, string>? names, Guid? id) =>
        names is not null && id.HasValue && names.TryGetValue(id.Value, out var name)
            ? name
            : null;
}

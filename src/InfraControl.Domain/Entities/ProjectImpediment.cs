using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Impedimento/risco do Projeto (ADR-043 · docs/38 §3.4).
/// </summary>
public sealed class ProjectImpediment : Entity
{
    public const int TitleMaxLength = 150;
    public const int DescriptionMaxLength = 2000;
    public const int MitigationMaxLength = 2000;

    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public ProjectImpedimentCategory Category { get; private set; }
    public ProjectImpedimentSeverity Severity { get; private set; }
    public string? MitigationPlan { get; private set; }
    public ProjectImpedimentStatus Status { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    private ProjectImpediment() { }

    public bool IsResolved => Status == ProjectImpedimentStatus.Resolved;

    public static ProjectImpediment Create(
        Guid projectId,
        string title,
        string description,
        ProjectImpedimentCategory category,
        ProjectImpedimentSeverity severity,
        string? mitigationPlan = null)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException(
                "O projeto do impedimento é obrigatório.", nameof(projectId));
        if (!Enum.IsDefined(category))
            throw new ArgumentException(
                "A categoria do impedimento é inválida.", nameof(category));
        if (!Enum.IsDefined(severity))
            throw new ArgumentException(
                "A severidade do impedimento é inválida.", nameof(severity));

        var impediment = new ProjectImpediment
        {
            ProjectId = projectId,
            Category = category,
            Severity = severity,
            Status = ProjectImpedimentStatus.Open
        };
        impediment.Apply(title, description, mitigationPlan);
        return impediment;
    }

    public void Update(
        string title,
        string description,
        ProjectImpedimentCategory category,
        ProjectImpedimentSeverity severity,
        string? mitigationPlan)
    {
        if (Status == ProjectImpedimentStatus.Resolved)
            throw new InvalidOperationException(
                "Não é possível alterar um impedimento já resolvido.");

        if (!Enum.IsDefined(category))
            throw new ArgumentException(
                "A categoria do impedimento é inválida.", nameof(category));
        if (!Enum.IsDefined(severity))
            throw new ArgumentException(
                "A severidade do impedimento é inválida.", nameof(severity));

        Category = category;
        Severity = severity;
        Apply(title, description, mitigationPlan);
    }

    public void Resolve()
    {
        if (Status == ProjectImpedimentStatus.Resolved)
            return;

        Status = ProjectImpedimentStatus.Resolved;
        ResolvedAt = AppTimeProvider.Current.GetUtcNow();
        Touch();
    }

    public void Reopen()
    {
        if (Status == ProjectImpedimentStatus.Open)
            return;

        Status = ProjectImpedimentStatus.Open;
        ResolvedAt = null;
        Touch();
    }

    private void Apply(string title, string description, string? mitigationPlan)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "O título do impedimento é obrigatório.", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException(
                "A descrição do impedimento é obrigatória.", nameof(description));

        title = title.Trim();
        if (title.Length > TitleMaxLength)
            throw new ArgumentException(
                $"O título do impedimento pode ter no máximo {TitleMaxLength} caracteres.",
                nameof(title));

        description = description.Trim();
        if (description.Length > DescriptionMaxLength)
            throw new ArgumentException(
                $"A descrição do impedimento pode ter no máximo {DescriptionMaxLength} caracteres.",
                nameof(description));

        if (mitigationPlan is { Length: > MitigationMaxLength })
            throw new ArgumentException(
                $"O plano de mitigação pode ter no máximo {MitigationMaxLength} caracteres.",
                nameof(mitigationPlan));

        Title = title;
        Description = description;
        MitigationPlan = string.IsNullOrWhiteSpace(mitigationPlan)
            ? null
            : mitigationPlan.Trim();
        Touch();
    }
}

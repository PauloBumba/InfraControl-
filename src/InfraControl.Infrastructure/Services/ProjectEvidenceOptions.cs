namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Local de armazenamento das evidências do módulo de projetos.
/// </summary>
public sealed class ProjectEvidenceOptions
{
    public const string SectionName = "ProjectEvidence";

    public string StoragePath { get; set; } = "var/project-evidence";
}

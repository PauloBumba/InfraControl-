namespace InfraControl.Domain.Enums;

/// <summary>Tipos de evidência técnica do Projeto (ADR-043 · docs/38 §3.4).</summary>
public enum ProjectEvidenceType
{
    TestReport = 1,
    ConfigurationBackup = 2,
    ArchitectureDiagram = 3,
    AcceptanceMinutes = 4,
    Other = 5
}

/// <summary>Rótulos pt-BR do <see cref="ProjectEvidenceType"/>.</summary>
public static class ProjectEvidenceTypeLabels
{
    public static string ToDisplayLabel(this ProjectEvidenceType type)
        => type switch
        {
            ProjectEvidenceType.TestReport => "relatório de teste",
            ProjectEvidenceType.ConfigurationBackup => "backup de configuração",
            ProjectEvidenceType.ArchitectureDiagram => "diagrama de arquitetura",
            ProjectEvidenceType.AcceptanceMinutes => "ata de homologação",
            ProjectEvidenceType.Other => "outro",
            _ => "evidência"
        };
}

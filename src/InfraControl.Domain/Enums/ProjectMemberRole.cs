namespace InfraControl.Domain.Enums;

/// <summary>
/// Papel do participante na equipe do Projeto (ADR-043 · docs/38 §3.3).
/// </summary>
public enum ProjectMemberRole
{
    Leader = 0,
    Member = 1,
    Reviewer = 2,
}

namespace InfraControl.Domain.Enums;

/// <summary>
/// Níveis de prioridade de um Projeto (ADR-043 · docs/38 §3.1).
/// </summary>
public enum ProjectPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
}

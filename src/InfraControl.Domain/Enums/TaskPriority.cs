namespace InfraControl.Domain.Enums;

/// <summary>
/// Prioridade da Task (F23-T001 — Plano de Execução §5). Os 4 níveis
/// espelham <see cref="IncidentSeverity"/> para manter o vocabulário da
/// plataforma; persistida como string(20) em <c>tasks.Priority</c>.
/// </summary>
public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

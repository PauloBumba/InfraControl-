namespace InfraControl.Domain.Enums;

/// <summary>
/// Estado da solicitação de conclusão de um Projeto (spec evolução 2026-11-07).
/// Pending → Approved | Rejected | ChangesRequested; uma nova submissão gera
/// um novo registro (o histórico de decisões fica preservado).
/// </summary>
public enum ProjectConclusionStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    ChangesRequested = 4,
}

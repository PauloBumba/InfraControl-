namespace InfraControl.Application.Projects;

/// <summary>
/// DTO de leitura/escrita da fase do Projeto (ADR-043 · F28-T003).
/// </summary>
public sealed record ProjectPhaseDto(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    int Order,
    Guid? ResponsibleId,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    DateOnly? ActualStartDate,
    DateOnly? ActualEndDate,
    string? CompletionCriteria,
    string Status,
    int Percent);

/// <summary>
/// Resumo das fases de um projeto, incluindo progresso global ponderado.
/// </summary>
public sealed record ProjectPhasesSummaryDto(
    Guid ProjectId,
    int OverallProgressPercent,
    IReadOnlyCollection<ProjectPhaseDto> Phases);

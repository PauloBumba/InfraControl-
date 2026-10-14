namespace InfraControl.Application.Reports.Transversal;

/// <summary>
/// Relatório transversal (ADR-049) — visão agregada de cima dos 5 módulos
/// (produtividade/PDI, tasks, knowledge, projetos, incidentes). Read-model
/// composto, sem armazenamento novo; contagens por status + tempo médio de
/// resolução de incidente. Não substitui os dashboards por vertical.
/// </summary>
public sealed record TransversalReportDto(
    DateOnly? From,
    DateOnly? To,
    PdiReportBlock Pdi,
    TaskReportBlock Tasks,
    KnowledgeReportBlock Knowledge,
    ProjectReportBlock Projects,
    IncidentReportBlock Incidents);

/// <summary>Planos de desenvolvimento individual (§18–§20 do PDI).</summary>
public sealed record PdiReportBlock(
    int Total,
    IReadOnlyDictionary<string, int> ByStatus,
    int Completed);

/// <summary>Tarefas operacionais.</summary>
public sealed record TaskReportBlock(
    int Total,
    IReadOnlyDictionary<string, int> ByState,
    int Completed,
    int Overdue);

/// <summary>Base de conhecimento.</summary>
public sealed record KnowledgeReportBlock(
    int Total,
    int Published,
    int Draft);

/// <summary>Projetos de gestão.</summary>
public sealed record ProjectReportBlock(
    int Total,
    IReadOnlyDictionary<string, int> ByStatus);

/// <summary>Incidentes — total, resolvidos e tempo médio de resolução.</summary>
public sealed record IncidentReportBlock(
    int Total,
    int Resolved,
    double? AverageResolutionHours);

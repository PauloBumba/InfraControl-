namespace InfraControl.Application.Reports.Crossings;

/// <summary>
/// F25-T002 (docs/33 §5) — indicadores integrados/cruzamentos entre módulos.
/// Cada par descreve quantas entidades de origem se ligam a alvos distintos
/// e quantos vínculos existem no total. Os vínculos N:N já prontos são
/// <c>task_incidents</c>, <c>task_knowledge_articles</c>,
/// <c>knowledge_article_origins</c> (Pdi), <c>knowledge_article_incidents</c>
/// e <c>task_pdis</c>.
/// </summary>
public sealed record ReportCrossingsDto(
    DateOnly? From,
    DateOnly? To,
    CrossingStatDto IncidentsToTasks,
    CrossingStatDto TasksToKnowledge,
    CrossingStatDto PdiToKnowledge,
    CrossingStatDto KnowledgeToIncidents,
    IReadOnlyList<CompetencyDemandDto> CompetenciesDemandedByTasks);

/// <summary>
/// Estatística de um par de cruzamento: <c>Sources</c> = origens com ≥1
/// vínculo no período; <c>Targets</c> = alvos distintos alcançados;
/// <c>Links</c> = total de linhas de vínculo.
/// </summary>
public sealed record CrossingStatDto(int Sources, int Targets, int Links);

/// <summary>
/// §5 "Task → PDI": competência exigida por tasks vinculadas ao PDI de um
/// usuário (<c>task_pdis</c> → objetivo → competência), com a contagem de
/// tasks que a demandam.
/// </summary>
public sealed record CompetencyDemandDto(Guid CompetencyId, string Name, int Tasks);

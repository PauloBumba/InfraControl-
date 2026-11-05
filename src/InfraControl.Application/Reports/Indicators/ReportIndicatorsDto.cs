namespace InfraControl.Application.Reports.Indicators;

/// <summary>
/// F25-T001 (spec docs/33 §2–§4) — indicadores detalhados por módulo.
/// Read-model composto, sem armazenamento novo (mesmo precedente do
/// transversal ADR-049). Só o que existe no modelo: sem campo de
/// equipe/setor em Task, sem visualização/busca em Knowledge —
/// indicadores derivam de estado, histórico, vínculos e revisão.
/// </summary>
public sealed record ReportIndicatorsDto(
    DateOnly? From,
    DateOnly? To,
    TaskIndicatorsDto Tasks,
    PdiIndicatorsBlockDto Pdi,
    KnowledgeIndicatorsDto Knowledge);

// ── §2 Tasks ─────────────────────────────────────────────────────────────

/// <summary>Indicadores de Tasks — volume, execução, distribuição, resultado.</summary>
public sealed record TaskIndicatorsDto(
    int Total,
    IReadOnlyDictionary<string, int> ByState,
    int CompletionRatePercent,
    int Overdue,
    int Blocked,
    int Replannings,
    int Impediments,
    int ImpedimentsResolved,
    int Updates,
    int WithResult,
    IReadOnlyDictionary<string, int> ByPriority,
    IReadOnlyDictionary<string, int> ByKind,
    IReadOnlyList<TaskResponsibleShareDto> ByResponsible);

/// <summary>Participação de um responsável nas tasks (ordenada por volume).</summary>
public sealed record TaskResponsibleShareDto(
    Guid? ResponsibleId,
    string? ResponsibleName,
    int Count,
    int Percent);

// ── §3 PDI ───────────────────────────────────────────────────────────────

/// <summary>Bloco de indicadores de PDI (situação/ desenvolvimento/ dificuldades).</summary>
public sealed record PdiIndicatorsBlockDto(
    int TotalPlans,
    IReadOnlyDictionary<string, int> ByStatus,
    int CompletedPlans,
    int CompletionRatePercent,
    int ObjectivesTotal,
    int CompetenciesWorked,
    int TotalImpediments,
    int ImpedimentsResolved);

// ── §4 Knowledge ─────────────────────────────────────────────────────────

/// <summary>Indicadores de Knowledge — produção, governança, evolução.</summary>
public sealed record KnowledgeIndicatorsDto(
    int Total,
    IReadOnlyDictionary<string, int> ByStatus,
    int Published,
    int InReview,
    int Draft,
    int PendingReview,
    int Obsolete,
    int Archived,
    IReadOnlyDictionary<string, int> ByOrigin,
    int Versioned,
    IReadOnlyList<KnowledgeTagShareDto> TopTags);

/// <summary>Artigos que carregam uma tag (ordenados por uso).</summary>
public sealed record KnowledgeTagShareDto(string Tag, int Count);

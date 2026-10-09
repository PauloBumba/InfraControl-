namespace InfraControl.Application.Pdi;

// ------------------------------------------------------------------
// Analytics (docs/30 §21, §22 — Fase 8, ADR-038): indicadores
// consolidados, relatórios individual/semestral/de dificuldades e
// base dos exports PDF/Excel. Tudo leitura — sem mutação, sem migração.
// ------------------------------------------------------------------

/// <summary>§21 — indicadores do PDI em 4 blocos (escopo = período dos planos).</summary>
public sealed record PdiIndicatorsDto(
    PdiExecutionIndicatorsDto Execution,
    PdiDevelopmentIndicatorsDto Development,
    PdiAccompanimentIndicatorsDto Accompaniment,
    PdiDifficultyIndicatorsDto Difficulties);

/// <summary>§21 execução: planos, conclusão, atraso e fases.</summary>
public sealed record PdiExecutionIndicatorsDto(
    int TotalPlans,
    int CompletedPlans,
    int CompletionRatePercent,
    int OverduePlans,
    int PhasesCompleted,
    int PhasesOverdue,
    int? AvgCompletionDays);

/// <summary>§21 desenvolvimento: competências trabalhadas, objetivos, resultados (§16) e evolução por competência.</summary>
public sealed record PdiDevelopmentIndicatorsDto(
    int CompetenciesWorked,
    int ObjectivesTotal,
    int ResultsAtingido,
    int ResultsParcial,
    int ResultsNaoAtingido,
    int ResultsReplanejado,
    IReadOnlyList<PdiEvolutionItemDto> EvolutionByCompetency);

/// <summary>Linha da evolução por competência: objetivos e % médio das fases encadeadas (objetivo → estratégia → fase).</summary>
public sealed record PdiEvolutionItemDto(
    string Name,
    int Objectives,
    int AvgPhasePercent);

/// <summary>§21 acompanhamento: check-ins, feedbacks e evidências validadas.</summary>
public sealed record PdiAccompanimentIndicatorsDto(
    int CheckIns,
    double CheckInsPerPlan,
    int Feedbacks,
    int EvidencesValidated);

/// <summary>§21 dificuldades: total, distribuição por tipo (texto livre) e tempo médio de resolução.</summary>
public sealed record PdiDifficultyIndicatorsDto(
    int TotalImpediments,
    IReadOnlyList<PdiDifficultyShareDto> ByType,
    int? AvgResolutionDays);

/// <summary>Fatia por tipo de impedimento — percentual arredondado sobre o total do escopo.</summary>
public sealed record PdiDifficultyShareDto(
    string Type,
    int Count,
    int Percent);

/// <summary>
/// §22 — relatório individual: colaborador · gestor · período ·
/// objetivos/estratégias/fases/atividades · acompanhamento · avaliação
/// final e resultado (composição das 3 leituras existentes).
/// </summary>
public sealed record PdiIndividualReportDto(
    string OwnerName,
    string OwnerEmail,
    string? ManagerName,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    PdiStructureDto Structure,
    PdiAccompanimentDto Accompaniment,
    PdiEvaluationDto Evaluation);

/// <summary>§22 — relatório semestral: consolido do período + tendências vs. janela anterior.</summary>
public sealed record PdiSemesterReportDto(
    string Label,
    DateOnly? From,
    DateOnly? To,
    int PlansInPeriod,
    int PlansCreatedInPeriod,
    int CompletedInPeriod,
    int CompletionRatePercent,
    int? AvgCompletionDays,
    PdiSemesterComparisonDto Trends,
    IReadOnlyList<PdiSemesterManagerDto> Managers,
    IReadOnlyList<PdiSemesterCompetencyDto> Competencies,
    IReadOnlyList<PdiDifficultyShareDto> Difficulties);

/// <summary>Comparação da janela atual com a janela anterior de mesmo tamanho.</summary>
public sealed record PdiSemesterComparisonDto(
    int PreviousPlansInPeriod,
    int PreviousCompleted,
    int PreviousCompletionRatePercent,
    int PlansDelta,
    int CompletedDelta,
    int CompletionRateDelta);

/// <summary>Linha de gestor do relatório semestral (§22 — "gestores").</summary>
public sealed record PdiSemesterManagerDto(
    string ManagerName,
    int Plans,
    int Completed);

/// <summary>Linha de competência do relatório semestral (§22 — "competências").</summary>
public sealed record PdiSemesterCompetencyDto(
    string Name,
    int Objectives,
    int AvgPhasePercent);

/// <summary>
/// §22 — relatório de dificuldades: distribuição por tipo no período
/// (ex. "38% das dificuldades relacionadas à capacitação").
/// </summary>
public sealed record PdiDifficultiesReportDto(
    DateOnly? From,
    DateOnly? To,
    int Total,
    IReadOnlyList<PdiDifficultyShareDto> ByType,
    int Resolved,
    int? AvgResolutionDays);

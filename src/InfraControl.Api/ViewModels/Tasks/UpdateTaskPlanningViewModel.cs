namespace InfraControl.Api.ViewModels.Tasks;

/// <summary>
/// PUT de planejamento da task (F23-T003 — §5) — comando de
/// substituição: as 2 datas vão juntas; `null` limpa a data e fim antes
/// do início → 400 pt-BR (domínio).
/// </summary>
public sealed record UpdateTaskPlanningViewModel(
    DateOnly? PlannedStart = null,
    DateOnly? PlannedEnd = null);

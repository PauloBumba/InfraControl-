namespace InfraControl.Application.Tasks;

/// <summary>TaskItem na leitura (F7-T001; identificação F23-T001;
/// planejamento/resultado F23-T003; subtarefas/progresso F23-T004/P4a;
/// Knowledge F23-T005/P5a; PDI F23-T005/P5b; criador F31-T001).</summary>
public sealed record TaskDto(
    Guid Id,
    string Title,
    string? Description,
    string Kind,
    string State,
    DateTime OpenedAt,
    int AssigneeCount,
    Guid[] AssigneeIds,
    int DeviceCount,
    Guid[] DeviceIds,
    int IncidentCount,
    Guid[] IncidentIds,
    int ArticleCount,
    Guid[] ArticleIds,
    int PdiCount,
    Guid[] PdiUserIds,
    string Priority,
    DateOnly? DueDate,
    Guid? ResponsibleId,
    string? ResponsibleName,
    DateOnly? PlannedStart,
    DateOnly? PlannedEnd,
    string? Result,
    Guid? ParentTaskId,
    string? ParentTitle,
    int SubtaskCount,
    int? Progress,
    Guid? CreatedById = null,
    string? CreatedByName = null)
{
    internal static TaskDto From(
        Domain.Entities.TaskItem task,
        IReadOnlyCollection<Guid>? assigneeIds = null,
        IReadOnlyCollection<Guid>? deviceIds = null,
        IReadOnlyCollection<Guid>? incidentIds = null,
        string? responsibleName = null,
        Ports.TaskSubtaskStats? subtaskStats = null,
        string? parentTitle = null,
        IReadOnlyCollection<Guid>? articleIds = null,
        IReadOnlyCollection<Guid>? pdiUserIds = null,
        string? createdByName = null)
        => new(
            task.Id,
            task.Title,
            task.Description,
            task.Kind.ToString(),
            task.State.ToString(),
            task.CreateDateTime,
            assigneeIds?.Count ?? 0,
            assigneeIds?.ToArray() ?? [],
            deviceIds?.Count ?? 0,
            deviceIds?.ToArray() ?? [],
            incidentIds?.Count ?? 0,
            incidentIds?.ToArray() ?? [],
            articleIds?.Count ?? 0,
            articleIds?.ToArray() ?? [],
            pdiUserIds?.Count ?? 0,
            pdiUserIds?.ToArray() ?? [],
            task.Priority.ToString(),
            task.DueDate,
            task.ResponsibleId,
            // Responsável sem usuário correspondente (excluído) → null.
            task.ResponsibleId is null
                ? null
                : string.IsNullOrEmpty(responsibleName) ? null : responsibleName,
            task.PlannedStart,
            task.PlannedEnd,
            task.Result,
            task.ParentTaskId,
            task.ParentTaskId is null ? null : parentTitle,
            subtaskStats?.Total ?? 0,
            // F23-T004/P4a — progresso DERIVADO das subtarefas: concluídas
            // (Resolved/Closed) sobre as não canceladas; sem subtarefas
            // ativas → null ("sem progresso a exibir"). Cancelled fica de
            // fora do denominador (trabalho abandonado não é "falta").
            subtaskStats is { NonCancelled: > 0 } stats
                ? (int)Math.Round(
                    100d * stats.Done / stats.NonCancelled,
                    MidpointRounding.AwayFromZero)
                : null,
            // F31-T001 — criador (task legada sem preenchimento → null).
            task.CreatedById,
            task.CreatedById is null
                ? null
                : string.IsNullOrEmpty(createdByName) ? null : createdByName);
}

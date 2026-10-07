namespace InfraControl.Application.Tasks;

/// <summary>Critério de aceite da Task na leitura (F23-T003 — §12).</summary>
public sealed record TaskCriterionDto(
    Guid Id,
    Guid TaskId,
    string Description,
    bool IsDone,
    DateTime? DoneAt,
    DateTime CreatedAt);

namespace InfraControl.Application.Tasks;

/// <summary>TaskPhaseDto — etapa dentro da task (F23-T004/P4d — §7).</summary>
public sealed record TaskPhaseDto(
    Guid Id,
    Guid TaskId,
    string Name,
    int Position,
    bool IsCurrent,
    DateTime? CompletedAt);

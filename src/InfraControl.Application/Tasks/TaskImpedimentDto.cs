namespace InfraControl.Application.Tasks;

/// <summary>TaskImpedimentDto — impedimento estruturado (F23-T004/P4b — §10).</summary>
public sealed record TaskImpedimentDto(
    Guid Id,
    Guid TaskId,
    string Description,
    DateTime CreatedAt,
    DateTime? ResolvedAt);

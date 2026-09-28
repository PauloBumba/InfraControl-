namespace InfraControl.Application.Sla.TaskSlas;

/// <summary>
/// Payload de TaskSla devolvido pela API — snapshot dos prazos históricos.
/// </summary>
public sealed record TaskSlaDto(
    Guid TaskId,
    Guid SlaPolicyId,
    int ResponseMinutes,
    int ResolutionMinutes,
    DateTime ResponseDueAt,
    DateTime ResolutionDueAt);

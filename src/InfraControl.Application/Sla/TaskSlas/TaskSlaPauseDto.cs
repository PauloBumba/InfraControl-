namespace InfraControl.Application.Sla.TaskSlas;

/// <summary>
/// Payload de TaskSlaPause devolvido pela API (sucesso → data do envelope).
/// </summary>
public sealed record TaskSlaPauseDto(
    Guid Id,
    Guid TaskId,
    DateTime StartAt,
    DateTime? EndAt);

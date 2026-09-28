namespace InfraControl.Application.Sla.Slos;

/// <summary>
/// Payload de Slo devolvido pela API (sucesso → data do envelope).
/// </summary>
public sealed record SloDto(
    Guid Id,
    string Name,
    decimal TargetPercent,
    int WindowDays,
    Guid ServiceId);

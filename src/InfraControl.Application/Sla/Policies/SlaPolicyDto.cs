namespace InfraControl.Application.Sla.Policies;

/// <summary>
/// Payload de SlaPolicy devolvido pela API (sucesso → data do envelope).
/// </summary>
public sealed record SlaPolicyDto(
    Guid Id,
    string Name,
    int ResponseMinutes,
    int ResolutionMinutes,
    Guid? ServiceId);

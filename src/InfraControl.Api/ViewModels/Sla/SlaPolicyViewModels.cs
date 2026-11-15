namespace InfraControl.Api.ViewModels.Sla;

/// <summary>Criação de política de SLA (F8-T002). `IsDefault` = política
/// padrão global, auto-aplicada a toda task nova.</summary>
public sealed record CreateSlaPolicyViewModel(
    string Name,
    int ResponseMinutes,
    int ResolutionMinutes,
    Guid? ServiceId,
    bool IsDefault = false);

/// <summary>Atualização de política de SLA (F8-T002).</summary>
public sealed record UpdateSlaPolicyViewModel(
    string Name,
    int ResponseMinutes,
    int ResolutionMinutes,
    Guid? ServiceId,
    bool IsDefault = false);

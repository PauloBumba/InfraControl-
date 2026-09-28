namespace InfraControl.Api.ViewModels.Sla;

/// <summary>Criação de política de SLA (F8-T002).</summary>
public sealed record CreateSlaPolicyViewModel(
    string Name,
    int ResponseMinutes,
    int ResolutionMinutes,
    Guid? ServiceId);

/// <summary>Atualização de política de SLA (F8-T002).</summary>
public sealed record UpdateSlaPolicyViewModel(
    string Name,
    int ResponseMinutes,
    int ResolutionMinutes,
    Guid? ServiceId);

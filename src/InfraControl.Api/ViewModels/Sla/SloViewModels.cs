namespace InfraControl.Api.ViewModels.Sla;

/// <summary>Criação de SLO (F8-T005).</summary>
public sealed record CreateSloViewModel(
    string Name,
    decimal TargetPercent,
    int WindowDays,
    Guid ServiceId);

/// <summary>Atualização de SLO (F8-T005).</summary>
public sealed record UpdateSloViewModel(
    string Name,
    decimal TargetPercent,
    int WindowDays,
    Guid ServiceId);

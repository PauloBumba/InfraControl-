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

/// <summary>Registro de medição de SLO (F8-T006).</summary>
public sealed record RecordSloMeasurementViewModel(
    DateTime MeasuredAt,
    decimal ValuePercent);

/// <summary>Registro de orçamento de SLO (F8-T007).</summary>
public sealed record RecordSloBudgetViewModel(
    DateTime PeriodStart,
    DateTime? PeriodEnd,
    decimal ConsumedPercent);

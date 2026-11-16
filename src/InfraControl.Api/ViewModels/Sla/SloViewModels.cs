namespace InfraControl.Api.ViewModels.Sla;

/// <summary>Criação de SLO (F8-T005 + ADR-068: monitoringCheckIds opcionais).</summary>
public sealed record CreateSloViewModel(
    string Name,
    decimal TargetPercent,
    int WindowDays,
    Guid ServiceId,
    IReadOnlyCollection<Guid>? MonitoringCheckIds = null);

/// <summary>Atualização de SLO (F8-T005 + ADR-068: monitoringCheckIds = replace; null mantém).</summary>
public sealed record UpdateSloViewModel(
    string Name,
    decimal TargetPercent,
    int WindowDays,
    Guid ServiceId,
    IReadOnlyCollection<Guid>? MonitoringCheckIds = null);

/// <summary>Registro de medição de SLO (F8-T006).</summary>
public sealed record RecordSloMeasurementViewModel(
    DateTime MeasuredAt,
    decimal ValuePercent);

/// <summary>Registro de orçamento de SLO (F8-T007).</summary>
public sealed record RecordSloBudgetViewModel(
    DateTime PeriodStart,
    DateTime? PeriodEnd,
    decimal ConsumedPercent);

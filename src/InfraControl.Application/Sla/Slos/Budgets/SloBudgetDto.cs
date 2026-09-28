namespace InfraControl.Application.Sla.Slos.Budgets;

/// <summary>
/// Payload de SloBudget devolvido pela API.
/// </summary>
public sealed record SloBudgetDto(
    Guid Id,
    Guid SloId,
    DateTime PeriodStart,
    DateTime? PeriodEnd,
    decimal ConsumedPercent);

namespace InfraControl.Application.Pdi;

using InfraControl.Domain.Enums;

/// <summary>
/// PDI (Plano de Desenvolvimento Individual) — espelha o formato do
/// <c>TaskDto</c>: enum vira string ("Active"…) no contrato JSON
/// (ciclo de vida de 9 estados, ADR-029).
/// </summary>
public sealed record PdiDto(
    Guid UserId,
    string OwnerName,
    string OwnerEmail,
    string Strategy,
    DateOnly TargetDate,
    string Status,
    Guid? ManagerId,
    string? ManagerName,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    DateTime UpdateDateTime)
{
    internal static PdiDto From(
        Domain.Entities.IndividualDevelopmentPlan plan,
        string ownerName,
        string ownerEmail,
        string? managerName = null)
        => new(
            plan.UserId,
            ownerName,
            ownerEmail,
            plan.Strategy,
            plan.TargetDate,
            plan.Status.ToString(),
            plan.ManagerId,
            managerName,
            plan.PeriodStart,
            plan.PeriodEnd,
            plan.UpdateDateTime);
}

/// <summary>
/// Entrada do timeline do PDI (GET /pdi/{userId}/history) — tipo e
/// status como string, ator com nome resolvido (null = removido).
/// </summary>
public sealed record PdiHistoryDto(
    Guid Id,
    string Type,
    Guid ActorId,
    string? ActorName,
    string? FromStatus,
    string? ToStatus,
    string? Reason,
    string? Detail,
    DateTime OccurredAt)
{
    internal static PdiHistoryDto From(
        Domain.Entities.PdiHistoryEntry entry,
        string? actorName)
        => new(
            entry.Id,
            entry.Type.ToString(),
            entry.ActorId,
            actorName,
            entry.FromStatus?.ToString(),
            entry.ToStatus?.ToString(),
            entry.Reason,
            entry.Detail,
            entry.CreateDateTime);
}

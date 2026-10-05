namespace InfraControl.Application.Pdi;

using InfraControl.Domain.Enums;

/// <summary>
/// PDI (Plano de Desenvolvimento Individual) — espelha o formato do
/// <c>TaskDto</c>: enum vira string ("InProgress"…) no contrato JSON.
/// </summary>
public sealed record PdiDto(
    Guid UserId,
    string OwnerName,
    string OwnerEmail,
    string Strategy,
    DateOnly TargetDate,
    string Status,
    DateTime UpdateDateTime)
{
    internal static PdiDto From(
        Domain.Entities.IndividualDevelopmentPlan plan,
        string ownerName,
        string ownerEmail)
        => new(
            plan.UserId,
            ownerName,
            ownerEmail,
            plan.Strategy,
            plan.TargetDate,
            plan.Status.ToString(),
            plan.UpdateDateTime);
}

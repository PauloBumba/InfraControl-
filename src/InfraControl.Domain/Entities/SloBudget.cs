using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Consumo do erro budget do SLO em uma janela (F8-T007) — quanto do
/// orçamento de erro permitido pelo `TargetPercent` já foi gasto
/// (docs/04: "orçamento em SloBudget").
/// </summary>
public sealed class SloBudget : AggregateRoot
{
    public Guid SloId { get; private set; }
    public DateTime PeriodStart { get; private set; }
    public DateTime? PeriodEnd { get; private set; }
    public decimal ConsumedPercent { get; private set; }

    private SloBudget() { }

    /// <summary>ADR-030 §5 — correção de registro (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new SloBudgetDeletedDomainEvent(Id, SloId));

    public static SloBudget Create(
        Guid sloId,
        DateTime periodStart,
        DateTime? periodEnd,
        decimal consumedPercent)
    {
        if (sloId == Guid.Empty)
            throw new ArgumentException("O id do SLO é obrigatório.", nameof(sloId));

        if (consumedPercent < 0 || consumedPercent > 100)
            throw new ArgumentException(
                "O percentual consumido deve estar entre 0 e 100.",
                nameof(consumedPercent));

        if (periodEnd is { } end && end <= periodStart)
            throw new ArgumentException(
                "O fim do período deve ser posterior ao início.",
                nameof(periodEnd));

        var budget = new SloBudget
        {
            SloId = sloId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            ConsumedPercent = consumedPercent
        };
        budget.RaiseDomainEvent(new SloBudgetCreatedDomainEvent(
            budget.Id, budget.SloId, budget.ConsumedPercent));
        return budget;
    }
}

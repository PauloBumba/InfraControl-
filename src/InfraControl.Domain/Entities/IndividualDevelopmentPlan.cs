using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Plano de Desenvolvimento Individual (PDI) — docs/29 §8. Conceito de
/// pessoas: direção de crescimento de um usuário (Operador/Admin), com
/// estratégia, prazo e status. 1 registro vigente por usuário (índice
/// único em <see cref="UserId"/>); ciclo novo sobrescreve o anterior via
/// upsert — sem histórico de ciclos (decisão do usuário, 2026-10-03).
/// CRUD apenas: não há remoção — encerrar é <see cref="PdiStatus.Cancelled"/>.
/// Create/Update levantam os domain events distribuídos (ADR-005).
/// </summary>
public sealed class IndividualDevelopmentPlan : AggregateRoot
{
    public const int StrategyMaxLength = 4000;

    public Guid UserId { get; private set; }
    public string Strategy { get; private set; } = string.Empty;
    public DateOnly TargetDate { get; private set; }
    public PdiStatus Status { get; private set; }

    private IndividualDevelopmentPlan() { }

    public static IndividualDevelopmentPlan Create(
        Guid userId,
        string strategy,
        DateOnly targetDate,
        PdiStatus status,
        PdiChangeActor actor)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException(
                "O usuário dono do PDI é obrigatório.", nameof(userId));

        ArgumentNullException.ThrowIfNull(actor);

        var plan = new IndividualDevelopmentPlan { UserId = userId };
        plan.Apply(strategy, targetDate, status);
        plan.RaiseDomainEvent(new PdiCreatedDomainEvent(
            plan.UserId, plan.TargetDate, plan.Status,
            actor.Id, actor.Email, actor.OwnerEmail));
        return plan;
    }

    public void Update(
        string strategy,
        DateOnly targetDate,
        PdiStatus status,
        PdiChangeActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        Apply(strategy, targetDate, status);
        RaiseDomainEvent(new PdiUpdatedDomainEvent(
            UserId, TargetDate, Status,
            actor.Id, actor.Email, actor.OwnerEmail));
    }

    private void Apply(string strategy, DateOnly targetDate, PdiStatus status)
    {
        Strategy = RequireStrategy(strategy);

        if (targetDate == default)
            throw new ArgumentException(
                "O prazo do PDI é obrigatório.", nameof(targetDate));

        if (!Enum.IsDefined(status))
            throw new ArgumentException(
                "Status do PDI inválido.", nameof(status));

        TargetDate = targetDate;
        Status = status;
        Touch();
    }

    private static string RequireStrategy(string strategy)
    {
        if (string.IsNullOrWhiteSpace(strategy))
            throw new ArgumentException(
                "A estratégia do PDI é obrigatória.", nameof(strategy));

        strategy = strategy.Trim();
        if (strategy.Length > StrategyMaxLength)
            throw new ArgumentException(
                $"A estratégia do PDI pode ter no máximo {StrategyMaxLength} caracteres.",
                nameof(strategy));

        return strategy;
    }
}

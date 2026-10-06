using InfraControl.Application.Pdi.Ports;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Pdi;

/// <summary>
/// Cadeia de resolução de uma child até o plano dono (Fase 2,
/// ADR-032): cada nó guarda só o id do pai, então a checagem de
/// acesso sobre objetivo → estratégia → fase → atividade até o
/// <see cref="IndividualDevelopmentPlan"/> que tem o dono.
/// </summary>
internal static class PdiStructureLookup
{
    internal static async Task<IndividualDevelopmentPlan> PlanForObjectiveAsync(
        IPdiStructureRepository repository,
        IPdiRepository plans,
        Guid objectiveId,
        CancellationToken cancellationToken)
    {
        var objective = await repository.GetObjectiveAsync(objectiveId, cancellationToken)
            ?? throw new KeyNotFoundException($"Objetivo '{objectiveId}' não encontrado.");

        return await PlanForUserAsync(plans, objective.UserId, cancellationToken);
    }

    internal static async Task<IndividualDevelopmentPlan> PlanForStrategyAsync(
        IPdiStructureRepository repository,
        IPdiRepository plans,
        Guid strategyId,
        CancellationToken cancellationToken)
    {
        var strategy = await repository.GetStrategyAsync(strategyId, cancellationToken)
            ?? throw new KeyNotFoundException($"Estratégia '{strategyId}' não encontrada.");

        var objective = await repository.GetObjectiveAsync(strategy.ObjectiveId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Objetivo '{strategy.ObjectiveId}' não encontrado.");

        return await PlanForUserAsync(plans, objective.UserId, cancellationToken);
    }

    internal static async Task<IndividualDevelopmentPlan> PlanForPhaseAsync(
        IPdiStructureRepository repository,
        IPdiRepository plans,
        Guid phaseId,
        CancellationToken cancellationToken)
    {
        var phase = await repository.GetPhaseAsync(phaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Fase '{phaseId}' não encontrada.");

        var strategy = await repository.GetStrategyAsync(phase.StrategyId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Estratégia '{phase.StrategyId}' não encontrada.");

        var objective = await repository.GetObjectiveAsync(strategy.ObjectiveId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Objetivo '{strategy.ObjectiveId}' não encontrado.");

        return await PlanForUserAsync(plans, objective.UserId, cancellationToken);
    }

    internal static async Task<IndividualDevelopmentPlan> PlanForActivityAsync(
        IPdiStructureRepository repository,
        IPdiRepository plans,
        Guid activityId,
        CancellationToken cancellationToken)
    {
        var activity = await repository.GetActivityAsync(activityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Atividade '{activityId}' não encontrada.");

        var phase = await repository.GetPhaseAsync(activity.PhaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Fase '{activity.PhaseId}' não encontrada.");

        var strategy = await repository.GetStrategyAsync(phase.StrategyId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Estratégia '{phase.StrategyId}' não encontrada.");

        var objective = await repository.GetObjectiveAsync(strategy.ObjectiveId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Objetivo '{strategy.ObjectiveId}' não encontrado.");

        return await PlanForUserAsync(plans, objective.UserId, cancellationToken);
    }

    private static async Task<IndividualDevelopmentPlan> PlanForUserAsync(
        IPdiRepository plans,
        Guid userId,
        CancellationToken cancellationToken)
        => await plans.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"PDI do usuário '{userId}' não encontrado.");
}

using InfraControl.Application.Pdi.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// Estrutura do PDI (Fase 2, ADR-031): catálogo + árvore de
/// objetivos/estratégias/fases/atividades em listas planas (domínio sem
/// nav collections). Nome da competência comparado com LOWER() para ser
/// case-insensitive em Postgres e Sqlite (testes in-memory).
/// </summary>
public sealed class PdiStructureRepository(AppDbContext dbContext)
    : IPdiStructureRepository
{
    public async Task<IReadOnlyList<PdiCompetency>> ListCompetenciesAsync(
        CancellationToken cancellationToken = default)
        => await dbContext.PdiCompetencies
            .OrderBy(competency => competency.Name)
            .ToListAsync(cancellationToken);

    public Task<PdiCompetency?> GetCompetencyAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.PdiCompetencies
            .SingleOrDefaultAsync(competency => competency.Id == id, cancellationToken);

    public Task<bool> CompetencyNameExistsAsync(
        string name, Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLower();
        return dbContext.PdiCompetencies.AnyAsync(
            competency => competency.Name.ToLower() == normalized
                && (excludeId == null || competency.Id != excludeId),
            cancellationToken);
    }

    public Task<bool> CompetencyInUseAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.PdiObjectives.AnyAsync(
            objective => objective.CompetencyId == id, cancellationToken);

    public void AddCompetency(PdiCompetency competency)
        => dbContext.PdiCompetencies.Add(competency);

    public void RemoveCompetency(PdiCompetency competency)
        => dbContext.PdiCompetencies.Remove(competency);

    public Task<PdiObjective?> GetObjectiveAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.PdiObjectives
            .SingleOrDefaultAsync(objective => objective.Id == id, cancellationToken);

    public Task<PdiStrategy?> GetStrategyAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.PdiStrategies
            .SingleOrDefaultAsync(strategy => strategy.Id == id, cancellationToken);

    public Task<PdiPhase?> GetPhaseAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.PdiPhases
            .SingleOrDefaultAsync(phase => phase.Id == id, cancellationToken);

    public Task<PdiActivity?> GetActivityAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.PdiActivities
            .SingleOrDefaultAsync(activity => activity.Id == id, cancellationToken);

    public Task<bool> HasStrategiesAsync(
        Guid objectiveId, CancellationToken cancellationToken = default)
        => dbContext.PdiStrategies.AnyAsync(
            strategy => strategy.ObjectiveId == objectiveId, cancellationToken);

    public Task<bool> HasPhasesAsync(
        Guid strategyId, CancellationToken cancellationToken = default)
        => dbContext.PdiPhases.AnyAsync(
            phase => phase.StrategyId == strategyId, cancellationToken);

    public Task<bool> HasActivitiesAsync(
        Guid phaseId, CancellationToken cancellationToken = default)
        => dbContext.PdiActivities.AnyAsync(
            activity => activity.PhaseId == phaseId, cancellationToken);

    public async Task<int> NextObjectiveOrderAsync(
        Guid userId, CancellationToken cancellationToken = default)
        => ((await dbContext.PdiObjectives
            .Where(objective => objective.UserId == userId)
            .MaxAsync(objective => (int?)objective.Order, cancellationToken)) ?? -1) + 1;

    public async Task<int> NextStrategyOrderAsync(
        Guid objectiveId, CancellationToken cancellationToken = default)
        => ((await dbContext.PdiStrategies
            .Where(strategy => strategy.ObjectiveId == objectiveId)
            .MaxAsync(strategy => (int?)strategy.Order, cancellationToken)) ?? -1) + 1;

    public async Task<int> NextPhaseOrderAsync(
        Guid strategyId, CancellationToken cancellationToken = default)
        => ((await dbContext.PdiPhases
            .Where(phase => phase.StrategyId == strategyId)
            .MaxAsync(phase => (int?)phase.Order, cancellationToken)) ?? -1) + 1;

    public async Task<int> NextActivityOrderAsync(
        Guid phaseId, CancellationToken cancellationToken = default)
        => ((await dbContext.PdiActivities
            .Where(activity => activity.PhaseId == phaseId)
            .MaxAsync(activity => (int?)activity.Order, cancellationToken)) ?? -1) + 1;

    public async Task<PdiStructureRows> GetTreeAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var objectives = await dbContext.PdiObjectives
            .Where(objective => objective.UserId == userId)
            .OrderBy(objective => objective.Order)
            .ToListAsync(cancellationToken);

        var objectiveIds = objectives.Select(objective => objective.Id).ToArray();

        var strategies = await dbContext.PdiStrategies
            .Where(strategy => objectiveIds.Contains(strategy.ObjectiveId))
            .OrderBy(strategy => strategy.Order)
            .ToListAsync(cancellationToken);

        var strategyIds = strategies.Select(strategy => strategy.Id).ToArray();

        var phases = await dbContext.PdiPhases
            .Where(phase => strategyIds.Contains(phase.StrategyId))
            .OrderBy(phase => phase.Order)
            .ToListAsync(cancellationToken);

        var phaseIds = phases.Select(phase => phase.Id).ToArray();

        var activities = await dbContext.PdiActivities
            .Where(activity => phaseIds.Contains(activity.PhaseId))
            .OrderBy(activity => activity.Order)
            .ToListAsync(cancellationToken);

        return new PdiStructureRows(objectives, strategies, phases, activities);
    }

    public void AddObjective(PdiObjective objective)
        => dbContext.PdiObjectives.Add(objective);

    public void AddStrategy(PdiStrategy strategy)
        => dbContext.PdiStrategies.Add(strategy);

    public void AddPhase(PdiPhase phase)
        => dbContext.PdiPhases.Add(phase);

    public void AddActivity(PdiActivity activity)
        => dbContext.PdiActivities.Add(activity);

    public void RemoveObjective(PdiObjective objective)
        => dbContext.PdiObjectives.Remove(objective);

    public void RemoveStrategy(PdiStrategy strategy)
        => dbContext.PdiStrategies.Remove(strategy);

    public void RemovePhase(PdiPhase phase)
        => dbContext.PdiPhases.Remove(phase);

    public void RemoveActivity(PdiActivity activity)
        => dbContext.PdiActivities.Remove(activity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

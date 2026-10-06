using InfraControl.Application.Pdi.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// Estrutura do PDI (Fase 2, ADR-032): catálogo + árvore de
/// objetivos/estratégias/fases/atividades em listas planas (domínio sem
/// nav collections). Nome da competência comparado com LOWER() para ser
/// case-insensitive em Postgres e Sqlite (testes in-memory). Fase 3
/// (ADR-033): acompanhamento da fase (progresso/check-ins/impedimentos/
/// feedbacks) filtrado pelos PhaseIds do plano.
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

    public Task<PdiProgress?> GetProgressAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.PdiProgresses
            .SingleOrDefaultAsync(progress => progress.Id == id, cancellationToken);

    public Task<PdiCheckIn?> GetCheckInAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.PdiCheckIns
            .SingleOrDefaultAsync(checkIn => checkIn.Id == id, cancellationToken);

    public Task<PdiImpediment?> GetImpedimentAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.PdiImpediments
            .SingleOrDefaultAsync(impediment => impediment.Id == id, cancellationToken);

    public Task<PdiFeedback?> GetFeedbackAsync(
        Guid id, CancellationToken cancellationToken = default)
        => dbContext.PdiFeedbacks
            .SingleOrDefaultAsync(feedback => feedback.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PdiProgress>> ListProgressesAsync(
        IReadOnlyCollection<Guid> phaseIds, CancellationToken cancellationToken = default)
        => await dbContext.PdiProgresses
            .Where(progress => phaseIds.Contains(progress.PhaseId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PdiCheckIn>> ListCheckInsAsync(
        IReadOnlyCollection<Guid> phaseIds, CancellationToken cancellationToken = default)
        => await dbContext.PdiCheckIns
            .Where(checkIn => phaseIds.Contains(checkIn.PhaseId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PdiImpediment>> ListImpedimentsAsync(
        IReadOnlyCollection<Guid> phaseIds, CancellationToken cancellationToken = default)
        => await dbContext.PdiImpediments
            .Where(impediment => phaseIds.Contains(impediment.PhaseId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PdiFeedback>> ListFeedbacksAsync(
        IReadOnlyCollection<Guid> phaseIds, CancellationToken cancellationToken = default)
        => await dbContext.PdiFeedbacks
            .Where(feedback => phaseIds.Contains(feedback.PhaseId))
            .ToListAsync(cancellationToken);

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

    public void AddProgress(PdiProgress progress)
        => dbContext.PdiProgresses.Add(progress);

    public void AddCheckIn(PdiCheckIn checkIn)
        => dbContext.PdiCheckIns.Add(checkIn);

    public void AddImpediment(PdiImpediment impediment)
        => dbContext.PdiImpediments.Add(impediment);

    public void AddFeedback(PdiFeedback feedback)
        => dbContext.PdiFeedbacks.Add(feedback);

    public void RemoveObjective(PdiObjective objective)
        => dbContext.PdiObjectives.Remove(objective);

    public void RemoveStrategy(PdiStrategy strategy)
        => dbContext.PdiStrategies.Remove(strategy);

    public void RemovePhase(PdiPhase phase)
        => dbContext.PdiPhases.Remove(phase);

    public void RemoveActivity(PdiActivity activity)
        => dbContext.PdiActivities.Remove(activity);

    public void RemoveProgress(PdiProgress progress)
        => dbContext.PdiProgresses.Remove(progress);

    public void RemoveCheckIn(PdiCheckIn checkIn)
        => dbContext.PdiCheckIns.Remove(checkIn);

    public void RemoveImpediment(PdiImpediment impediment)
        => dbContext.PdiImpediments.Remove(impediment);

    public void RemoveFeedback(PdiFeedback feedback)
        => dbContext.PdiFeedbacks.Remove(feedback);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

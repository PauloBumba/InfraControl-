using InfraControl.Domain.Entities;

namespace InfraControl.Application.Pdi.Ports;

/// <summary>
/// PDI — estrutura do desenvolvimento (docs/30 §5-§8, Fase 2,
/// ADR-032): catálogo de competências + objetivos → estratégias →
/// fases → atividades. O plano (<see cref="IndividualDevelopmentPlan"/>)
/// continua sendo o agregado raiz: aqui só as children são persistidas.
/// Sem <c>IRepository&lt;T&gt;</c> marker — o RepositoryRegistrar aceita
/// portas sem o marcador (contrato único por nome).
/// </summary>
public interface IPdiStructureRepository
{
    // ---- Catálogo de competências (Admin escreve; leitura = Operate) ----
    Task<IReadOnlyList<PdiCompetency>> ListCompetenciesAsync(
        CancellationToken cancellationToken = default);

    Task<PdiCompetency?> GetCompetencyAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<bool> CompetencyNameExistsAsync(
        string name, Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Referenciada por algum objetivo? (delete → 409.)</summary>
    Task<bool> CompetencyInUseAsync(
        Guid id, CancellationToken cancellationToken = default);

    void AddCompetency(PdiCompetency competency);
    void RemoveCompetency(PdiCompetency competency);

    // ---- Nós da árvore (leituras pontuais para mutações) ----
    Task<PdiObjective?> GetObjectiveAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<PdiStrategy?> GetStrategyAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<PdiPhase?> GetPhaseAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<PdiActivity?> GetActivityAsync(
        Guid id, CancellationToken cancellationToken = default);

    // ---- Restrições de exclusão (children → 409) ----
    Task<bool> HasStrategiesAsync(
        Guid objectiveId, CancellationToken cancellationToken = default);

    Task<bool> HasPhasesAsync(
        Guid strategyId, CancellationToken cancellationToken = default);

    Task<bool> HasActivitiesAsync(
        Guid phaseId, CancellationToken cancellationToken = default);

    // ---- Próxima posição (criação anexa ao fim da lista) ----
    Task<int> NextObjectiveOrderAsync(
        Guid userId, CancellationToken cancellationToken = default);

    Task<int> NextStrategyOrderAsync(
        Guid objectiveId, CancellationToken cancellationToken = default);

    Task<int> NextPhaseOrderAsync(
        Guid strategyId, CancellationToken cancellationToken = default);

    Task<int> NextActivityOrderAsync(
        Guid phaseId, CancellationToken cancellationToken = default);

    // ---- Árvore completa de um plano (GET structure) ----
    Task<PdiStructureRows> GetTreeAsync(
        Guid userId, CancellationToken cancellationToken = default);

    // ---- Acompanhamento (Fase 3, ADR-033) ----
    Task<PdiProgress?> GetProgressAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<PdiCheckIn?> GetCheckInAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<PdiImpediment?> GetImpedimentAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<PdiFeedback?> GetFeedbackAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PdiProgress>> ListProgressesAsync(
        IReadOnlyCollection<Guid> phaseIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PdiCheckIn>> ListCheckInsAsync(
        IReadOnlyCollection<Guid> phaseIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PdiImpediment>> ListImpedimentsAsync(
        IReadOnlyCollection<Guid> phaseIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PdiFeedback>> ListFeedbacksAsync(
        IReadOnlyCollection<Guid> phaseIds, CancellationToken cancellationToken = default);

    // ---- Escrita ----
    void AddObjective(PdiObjective objective);
    void AddStrategy(PdiStrategy strategy);
    void AddPhase(PdiPhase phase);
    void AddActivity(PdiActivity activity);
    void AddProgress(PdiProgress progress);
    void AddCheckIn(PdiCheckIn checkIn);
    void AddImpediment(PdiImpediment impediment);
    void AddFeedback(PdiFeedback feedback);

    void RemoveObjective(PdiObjective objective);
    void RemoveStrategy(PdiStrategy strategy);
    void RemovePhase(PdiPhase phase);
    void RemoveActivity(PdiActivity activity);
    void RemoveProgress(PdiProgress progress);
    void RemoveCheckIn(PdiCheckIn checkIn);
    void RemoveImpediment(PdiImpediment impediment);
    void RemoveFeedback(PdiFeedback feedback);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Árvore em listas planas (o domínio não tem nav collections — o
/// handler reagrupa por FK, mesmo estilo do PdiRepository).
/// </summary>
public sealed record PdiStructureRows(
    IReadOnlyList<PdiObjective> Objectives,
    IReadOnlyList<PdiStrategy> Strategies,
    IReadOnlyList<PdiPhase> Phases,
    IReadOnlyList<PdiActivity> Activities);

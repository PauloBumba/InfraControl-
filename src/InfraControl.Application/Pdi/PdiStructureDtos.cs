namespace InfraControl.Application.Pdi;

/// <summary>
/// Estrutura do desenvolvimento (docs/30 §5-§8, Fase 2, ADR-032).
/// Status de fase como string no contrato JSON (mesmo critério do
/// <c>PdiDto</c>). Os nós aninhados carregam seus filhos em propriedade
/// <c>init</c> (a árvore montada no handler); a criação devolve o nó
/// com listas vazias — o front refaz o GET da árvore após mutação.
/// </summary>
public sealed record PdiCompetencyDto(Guid Id, string Name, string? Description);

public sealed record PdiObjectiveNodeDto(
    Guid Id,
    string Title,
    string? Description,
    Guid? CompetencyId,
    string? CompetencyName,
    string? CurrentSituation,
    string? DesiredSituation,
    string? ExpectedResult,
    string? Indicator,
    DateOnly? TargetDate,
    int Order)
{
    public IReadOnlyList<PdiStrategyNodeDto> Strategies { get; init; } = [];
}

public sealed record PdiStrategyNodeDto(
    Guid Id,
    string Title,
    string? Description,
    int Order)
{
    public IReadOnlyList<PdiPhaseNodeDto> Phases { get; init; } = [];
}

public sealed record PdiPhaseNodeDto(
    Guid Id,
    string Title,
    string? Description,
    Guid? ResponsibleId,
    string? ResponsibleName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? CompletionCriteria,
    string Status,
    int Percent,
    int Order)
{
    public IReadOnlyList<PdiActivityNodeDto> Activities { get; init; } = [];
}

public sealed record PdiActivityNodeDto(
    Guid Id,
    string Title,
    DateOnly? DueDate,
    bool Completed,
    int Order);

public sealed record PdiStructureDto(
    Guid UserId,
    IReadOnlyList<PdiObjectiveNodeDto> Objectives);

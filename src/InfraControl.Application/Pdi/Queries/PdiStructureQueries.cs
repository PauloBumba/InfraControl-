using InfraControl.Application.Pdi.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Pdi.Queries;

// Estrutura do desenvolvimento (docs/30 §5-§8, Fase 2, ADR-032):
// árvore objetivos → estratégias → fases → atividades do plano, com
// competência/responsável resolvidos; 404 sem plano, 403 para o
// Operador em PDI de outro (leitura = write-scoping, ADR-030).

public sealed record GetPdiStructureQuery(Guid UserId)
    : IQuery<PdiStructureDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class GetPdiStructureQueryHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetPdiStructureQuery, PdiStructureDto>
{
    public async Task<PdiStructureDto> Handle(
        GetPdiStructureQuery request, CancellationToken cancellationToken)
    {
        var isAdmin = currentUser.Roles.Contains(UserRole.Admin.ToString());
        if (!isAdmin && currentUser.UserId != request.UserId)
            throw new UnauthorizedAccessException(
                "Você só pode ver a estrutura do seu próprio PDI.");

        _ = await plans.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"PDI do usuário '{request.UserId}' não encontrado.");

        return await PdiStructureBuilder.BuildAsync(
            repository, plans, request.UserId, cancellationToken);
    }
}

/// <summary>
/// Constrói a árvore objetivos → estratégias → fases → atividades
/// (leitura pura, sem guard de acesso) — usada pelo GET structure e
/// pelo relatório individual (§22 — Fase 8).
/// </summary>
internal static class PdiStructureBuilder
{
    internal static async Task<PdiStructureDto> BuildAsync(
        IPdiStructureRepository repository,
        IPdiRepository plans,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rows = await repository.GetTreeAsync(userId, cancellationToken);

        var competencies = await repository.ListCompetenciesAsync(cancellationToken);
        var competencyNames = competencies.ToDictionary(
            competency => competency.Id, competency => competency.Name);

        // Nomes de responsáveis resolvidos uma única vez (join em
        // DomainUsers — mesma fonte usada no GET /pdi).
        var responsibleNames = new Dictionary<Guid, string?>();
        foreach (var responsibleId in rows.Phases
                     .Select(phase => phase.ResponsibleId)
                     .OfType<Guid>()
                     .Distinct())
        {
            responsibleNames[responsibleId] = await plans.GetUserNameAsync(
                responsibleId, cancellationToken);
        }

        var strategiesByObjective = rows.Strategies
            .GroupBy(strategy => strategy.ObjectiveId)
            .ToDictionary(group => group.Key,
                group => group.OrderBy(strategy => strategy.Order).ToList());

        var phasesByStrategy = rows.Phases
            .GroupBy(phase => phase.StrategyId)
            .ToDictionary(group => group.Key,
                group => group.OrderBy(phase => phase.Order).ToList());

        var activitiesByPhase = rows.Activities
            .GroupBy(activity => activity.PhaseId)
            .ToDictionary(group => group.Key,
                group => group.OrderBy(activity => activity.Order).ToList());

        var objectives = rows.Objectives
            .OrderBy(objective => objective.Order)
            .Select(objective =>
            {
                var node = new PdiObjectiveNodeDto(
                    objective.Id,
                    objective.Title,
                    objective.Description,
                    objective.CompetencyId,
                    objective.CompetencyId is { } competencyId
                        && competencyNames.TryGetValue(competencyId, out var competencyName)
                        ? competencyName
                        : null,
                    objective.CurrentSituation,
                    objective.DesiredSituation,
                    objective.ExpectedResult,
                    objective.Indicator,
                    objective.TargetDate,
                    objective.Order);

                var strategies = strategiesByObjective
                    .TryGetValue(objective.Id, out var list)
                    ? list
                    : [];

                return node with
                {
                    Strategies = strategies.Select(strategy =>
                    {
                        var strategyNode = new PdiStrategyNodeDto(
                            strategy.Id, strategy.Title, strategy.Description,
                            strategy.Order);

                        var phases = phasesByStrategy
                            .TryGetValue(strategy.Id, out var phaseList)
                            ? phaseList
                            : [];

                        return strategyNode with
                        {
                            Phases = phases.Select(phase =>
                            {
                                var phaseNode = new PdiPhaseNodeDto(
                                    phase.Id,
                                    phase.Title,
                                    phase.Description,
                                    phase.ResponsibleId,
                                    phase.ResponsibleId is { } responsibleId
                                    && responsibleNames.TryGetValue(responsibleId, out var responsibleName)
                                        ? responsibleName
                                        : null,
                                    phase.StartDate,
                                    phase.EndDate,
                                    phase.CompletionCriteria,
                                    phase.Status.ToString(),
                                    phase.Percent,
                                    phase.Order);

                                var activities = activitiesByPhase
                                    .TryGetValue(phase.Id, out var activityList)
                                    ? activityList
                                    : [];

                                return phaseNode with
                                {
                                    Activities = activities
                                        .Select(activity => new PdiActivityNodeDto(
                                            activity.Id, activity.Title,
                                            activity.DueDate, activity.Completed,
                                            activity.Order))
                                        .ToList()
                                };
                            }).ToList()
                        };
                    }).ToList()
                };
            })
            .ToList();

        return new PdiStructureDto(userId, objectives);
    }
}

public sealed record ListPdiCompetenciesQuery()
    : IQuery<IReadOnlyList<PdiCompetencyDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class ListPdiCompetenciesQueryHandler(IPdiStructureRepository repository)
    : IRequestHandler<ListPdiCompetenciesQuery, IReadOnlyList<PdiCompetencyDto>>
{
    public async Task<IReadOnlyList<PdiCompetencyDto>> Handle(
        ListPdiCompetenciesQuery request, CancellationToken cancellationToken)
    {
        var competencies = await repository.ListCompetenciesAsync(cancellationToken);
        return competencies
            .OrderBy(competency => competency.Name)
            .Select(competency => new PdiCompetencyDto(
                competency.Id, competency.Name, competency.Description))
            .ToList();
    }
}

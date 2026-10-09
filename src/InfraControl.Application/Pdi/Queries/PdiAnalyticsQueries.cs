using InfraControl.Application.Pdi.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Pdi.Queries;

// ------------------------------------------------------------------
// Analytics (docs/30 §21, §22 — Fase 8, ADR-038): indicadores,
// relatórios individual/semestral/de dificuldades e filtros de
// período. Escopo único = planos com TargetDate dentro de [de..até]
// (sem filtro = tudo); tudo leitura (sem migração), Admin guardado no
// handler (mesmo padrão do dashboard do §20 — Fase 7).
// ------------------------------------------------------------------

/// <summary>Regras de período/compartilhadas dos relatórios (§21, §22).</summary>
internal static class PdiAnalytics
{
    /// <summary>'de' posterior ao 'até' = 400 (ArgumentException).</summary>
    internal static void EnsurePeriod(DateOnly? from, DateOnly? to)
    {
        if (from is { } start && to is { } end && start > end)
            throw new ArgumentException(
                "O período 'de' não pode ser posterior ao 'até'.");
    }

    /// <summary>Alvo dentro da janela (lados nulos = sem limite).</summary>
    internal static bool InPeriod(DateOnly target, DateOnly? from, DateOnly? to)
        => (from is null || target >= from)
           && (to is null || target <= to);

    /// <summary>Percentual arredondado (AwayFromZero) — 0 quando base = 0.</summary>
    internal static int Percent(int part, int total)
        => total == 0
            ? 0
            : (int)Math.Round(
                100.0 * part / total, MidpointRounding.AwayFromZero);

    internal static void EnsureAdmin(ICurrentUserContext currentUser, string resource)
    {
        if (!currentUser.Roles.Contains(UserRole.Admin.ToString()))
            throw new UnauthorizedAccessException(
                $"Apenas administradores podem ver {resource}.");
    }

    /// <summary>
    /// Dias médios até a conclusão (§21): primeiro evento Completed do
    /// timeline − criação do plano; null quando nada foi concluído.
    /// </summary>
    internal static async Task<int?> AvgCompletionDaysAsync(
        IPdiRepository plans,
        IEnumerable<IndividualDevelopmentPlan> scoped,
        CancellationToken cancellationToken)
    {
        var days = new List<int>();
        foreach (var plan in scoped.Where(item =>
                     item.Status == PdiStatus.Completed))
        {
            var completedAt = (await plans.ListHistoryAsync(
                    plan.UserId, cancellationToken))
                .Where(row => row.Entry.ToStatus == PdiStatus.Completed)
                .Select(row => row.Entry.CreateDateTime)
                .OrderBy(moment => moment)
                .FirstOrDefault();

            if (completedAt == default)
                continue;

            var taken = DateOnly.FromDateTime(completedAt).DayNumber
                - DateOnly.FromDateTime(plan.CreateDateTime).DayNumber;
            if (taken >= 0)
                days.Add(taken);
        }

        return days.Count == 0 ? null : (int)Math.Round(days.Average());
    }

    /// <summary>
    /// Acumula objetivos e % de fases por competência (objetivo →
    /// estratégia → fase) — base da evolução do §21 e das competências
    /// do relatório semestral do §22.
    /// </summary>
    internal static void AccumulateEvolution(
        PdiStructureRows rows,
        Dictionary<Guid, int> objectiveCounts,
        Dictionary<Guid, List<int>> percentsByCompetency)
    {
        foreach (var strategy in rows.Strategies)
        {
            var objective = rows.Objectives.FirstOrDefault(item =>
                item.Id == strategy.ObjectiveId);
            if (objective?.CompetencyId is not { } competencyId)
                continue;

            foreach (var phase in rows.Phases.Where(item =>
                         item.StrategyId == strategy.Id))
            {
                if (!percentsByCompetency.TryGetValue(competencyId, out var list))
                    percentsByCompetency[competencyId] = list = [];
                list.Add(phase.Percent);
            }
        }

        foreach (var objective in rows.Objectives)
        {
            if (objective.CompetencyId is not { } competencyId)
                continue;

            objectiveCounts[competencyId] =
                objectiveCounts.GetValueOrDefault(competencyId) + 1;
            if (!percentsByCompetency.ContainsKey(competencyId))
                percentsByCompetency[competencyId] = [];
        }
    }

    /// <summary>Fechamento da evolução: só competências com objetivos no escopo.</summary>
    internal static IReadOnlyList<PdiEvolutionItemDto> CloseEvolution(
        IReadOnlyDictionary<Guid, string> competencyNames,
        Dictionary<Guid, int> objectiveCounts,
        Dictionary<Guid, List<int>> percentsByCompetency)
        => objectiveCounts
            .Where(pair => pair.Value > 0)
            .Select(pair =>
            {
                var percents = percentsByCompetency
                    .GetValueOrDefault(pair.Key) ?? [];
                var avg = percents.Count == 0
                    ? 0
                    : (int)Math.Round(percents.Average());
                return new PdiEvolutionItemDto(
                    competencyNames.GetValueOrDefault(pair.Key)
                    ?? "Competência removida",
                    pair.Value, avg);
            })
            .OrderBy(item => item.Name)
            .ToList();

    /// <summary>Distribuição por tipo de impedimento + resolução (§21, §22).</summary>
    internal static (IReadOnlyList<PdiDifficultyShareDto> ByType, int Resolved, int? AvgResolutionDays)
        AggregateImpediments(IReadOnlyCollection<PdiImpediment> impediments)
    {
        var total = impediments.Count;
        var byType = impediments
            .GroupBy(item => item.Type)
            .Select(group => new PdiDifficultyShareDto(
                group.Key, group.Count(), Percent(group.Count(), total)))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Type)
            .ToList();

        var resolved = impediments.Count(item =>
            item.Status == PdiImpedimentStatus.Resolved);
        var resolutionDays = impediments
            .Where(item => item.ResolvedAt is not null)
            .Select(item =>
                item.ResolvedAt!.Value.DayNumber - item.OccurredAt.DayNumber)
            .Where(days => days >= 0)
            .ToList();
        int? avgResolution = resolutionDays.Count == 0
            ? null
            : (int)Math.Round(resolutionDays.Average());

        return (byType, resolved, avgResolution);
    }
}

/// <summary>§21 — indicadores consolidados (Admin; filtro de período opcional).</summary>
public sealed record GetPdiIndicatorsQuery(
    DateOnly? From = null,
    DateOnly? To = null)
    : IQuery<PdiIndicatorsDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed class GetPdiIndicatorsQueryHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetPdiIndicatorsQuery, PdiIndicatorsDto>
{
    public async Task<PdiIndicatorsDto> Handle(
        GetPdiIndicatorsQuery request, CancellationToken cancellationToken)
    {
        PdiAnalytics.EnsureAdmin(currentUser, "os indicadores do PDI");
        PdiAnalytics.EnsurePeriod(request.From, request.To);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var scoped = (await plans.ListWithOwnersAsync(
                onlyUserId: null, cancellationToken))
            .Where(item => PdiAnalytics.InPeriod(
                item.Plan.TargetDate, request.From, request.To))
            .Select(item => item.Plan)
            .ToList();

        var total = scoped.Count;
        var completed = scoped.Count(plan =>
            plan.Status == PdiStatus.Completed);
        var overdue = scoped.Count(plan =>
            plan.TargetDate < today
            && plan.Status is not (PdiStatus.Completed or PdiStatus.Cancelled));

        var competencies = await repository.ListCompetenciesAsync(cancellationToken);
        var competencyNames = competencies.ToDictionary(
            competency => competency.Id, competency => competency.Name);
        var objectiveCounts = competencyNames.Keys
            .ToDictionary(id => id, _ => 0);
        var percentsByCompetency = competencyNames.Keys
            .ToDictionary(id => id, _ => new List<int>());

        int phasesCompleted = 0;
        int phasesOverdue = 0;
        int checkIns = 0;
        int feedbacks = 0;
        int evidencesValidated = 0;
        int objectivesTotal = 0;
        int resultsAtingido = 0;
        int resultsParcial = 0;
        int resultsNaoAtingido = 0;
        int resultsReplanejado = 0;
        var impediments = new List<PdiImpediment>();

        foreach (var plan in scoped)
        {
            var rows = await repository.GetTreeAsync(
                plan.UserId, cancellationToken);
            var phaseIds = rows.Phases.Select(phase => phase.Id).ToList();

            phasesCompleted += rows.Phases.Count(phase =>
                phase.Status == PdiPhaseStatus.Completed);
            phasesOverdue += rows.Phases.Count(phase =>
                phase.EndDate is { } endDate && endDate < today
                && phase.Status != PdiPhaseStatus.Completed);

            checkIns += (await repository.ListCheckInsAsync(
                phaseIds, cancellationToken)).Count;
            feedbacks += (await repository.ListFeedbacksAsync(
                phaseIds, cancellationToken)).Count;
            evidencesValidated += (await repository.ListEvidenceAsync(
                    phaseIds, cancellationToken))
                .Count(evidence =>
                    evidence.Status == PdiEvidenceStatus.Validated);
            impediments.AddRange(await repository.ListImpedimentsAsync(
                phaseIds, cancellationToken));

            objectivesTotal += rows.Objectives.Count;
            PdiAnalytics.AccumulateEvolution(
                rows, objectiveCounts, percentsByCompetency);

            var result = await repository.GetResultAsync(
                plan.Id, cancellationToken);
            switch (result?.Outcome)
            {
                case PdiResultOutcome.Atingido:
                    resultsAtingido++;
                    break;
                case PdiResultOutcome.ParcialmenteAtingido:
                    resultsParcial++;
                    break;
                case PdiResultOutcome.NaoAtingido:
                    resultsNaoAtingido++;
                    break;
                case PdiResultOutcome.Replanejado:
                    resultsReplanejado++;
                    break;
            }
        }

        var evolution = PdiAnalytics.CloseEvolution(
            competencyNames, objectiveCounts, percentsByCompetency);
        var (byType, _, avgResolutionDays) =
            PdiAnalytics.AggregateImpediments(impediments);

        return new PdiIndicatorsDto(
            new PdiExecutionIndicatorsDto(
                total,
                completed,
                PdiAnalytics.Percent(completed, total),
                overdue,
                phasesCompleted,
                phasesOverdue,
                await PdiAnalytics.AvgCompletionDaysAsync(
                    plans, scoped, cancellationToken)),
            new PdiDevelopmentIndicatorsDto(
                evolution.Count,
                objectivesTotal,
                resultsAtingido,
                resultsParcial,
                resultsNaoAtingido,
                resultsReplanejado,
                evolution),
            new PdiAccompanimentIndicatorsDto(
                checkIns,
                total == 0
                    ? 0
                    : Math.Round(checkIns / (double)total, 1),
                feedbacks,
                evidencesValidated),
            new PdiDifficultyIndicatorsDto(
                impediments.Count, byType, avgResolutionDays));
    }
}

/// <summary>§22 — relatório individual do PDI (Admin/dono/gestor).</summary>
public sealed record GetPdiIndividualReportQuery(Guid UserId)
    : IQuery<PdiIndividualReportDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class GetPdiIndividualReportQueryHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetPdiIndividualReportQuery, PdiIndividualReportDto>
{
    public async Task<PdiIndividualReportDto> Handle(
        GetPdiIndividualReportQuery request,
        CancellationToken cancellationToken)
    {
        var plan = await plans.GetByUserIdAsync(
            request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"PDI do usuário '{request.UserId}' não encontrado.");
        PdiAccess.EnsureCanView(currentUser, plan);

        var owner = await plans.GetOwnerAsync(
            request.UserId, cancellationToken);
        string? managerName = plan.ManagerId is { } managerId
            ? await plans.GetUserNameAsync(managerId, cancellationToken)
            : null;

        var structure = await PdiStructureBuilder.BuildAsync(
            repository, plans, plan.UserId, cancellationToken);
        var accompaniment = await PdiAccompanimentBuilder.BuildAsync(
            repository, plans, plan, cancellationToken);
        var evaluation = await PdiEvaluationBuilder.BuildAsync(
            repository, plans, plan, cancellationToken);

        return new PdiIndividualReportDto(
            owner?.Name ?? string.Empty,
            owner?.Email ?? string.Empty,
            managerName,
            plan.PeriodStart,
            plan.PeriodEnd,
            structure,
            accompaniment,
            evaluation);
    }
}

/// <summary>§22 — relatório semestral (Admin; padrão = semestre corrente).</summary>
public sealed record GetPdiSemesterReportQuery(
    DateOnly? From = null,
    DateOnly? To = null)
    : IQuery<PdiSemesterReportDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed class GetPdiSemesterReportQueryHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetPdiSemesterReportQuery, PdiSemesterReportDto>
{
    public async Task<PdiSemesterReportDto> Handle(
        GetPdiSemesterReportQuery request, CancellationToken cancellationToken)
    {
        PdiAnalytics.EnsureAdmin(currentUser, "o relatório semestral do PDI");
        PdiAnalytics.EnsurePeriod(request.From, request.To);

        var (from, to) = DefaultWindow(request.From, request.To);

        var all = await plans.ListWithOwnersAsync(
            onlyUserId: null, cancellationToken);
        var scoped = all
            .Where(item => PdiAnalytics.InPeriod(
                item.Plan.TargetDate, from, to))
            .ToList();

        var total = scoped.Count;
        var completed = scoped.Count(item =>
            item.Plan.Status == PdiStatus.Completed);
        var createdInPeriod = all.Count(item =>
            PdiAnalytics.InPeriod(
                DateOnly.FromDateTime(item.Plan.CreateDateTime), from, to));

        var competencies = await repository.ListCompetenciesAsync(cancellationToken);
        var competencyNames = competencies.ToDictionary(
            competency => competency.Id, competency => competency.Name);
        var objectiveCounts = competencyNames.Keys
            .ToDictionary(id => id, _ => 0);
        var percentsByCompetency = competencyNames.Keys
            .ToDictionary(id => id, _ => new List<int>());

        var impediments = new List<PdiImpediment>();
        foreach (var item in scoped)
        {
            var rows = await repository.GetTreeAsync(
                item.Plan.UserId, cancellationToken);
            var phaseIds = rows.Phases.Select(phase => phase.Id).ToList();

            impediments.AddRange(await repository.ListImpedimentsAsync(
                phaseIds, cancellationToken));
            PdiAnalytics.AccumulateEvolution(
                rows, objectiveCounts, percentsByCompetency);
        }

        var evolution = PdiAnalytics.CloseEvolution(
            competencyNames, objectiveCounts, percentsByCompetency);
        var (byType, _, avgResolutionDays) =
            PdiAnalytics.AggregateImpediments(impediments);

        var managers = scoped
            .GroupBy(item => item.Plan.ManagerId)
            .Select(group => new PdiSemesterManagerDto(
                group.FirstOrDefault()?.ManagerName ?? "Sem gestor",
                group.Count(),
                group.Count(item =>
                    item.Plan.Status == PdiStatus.Completed)))
            .OrderByDescending(item => item.Plans)
            .ThenBy(item => item.ManagerName)
            .ToList();

        var trends = ComparePeriods(all, from, to);

        return new PdiSemesterReportDto(
            Label(from, to),
            from,
            to,
            total,
            createdInPeriod,
            completed,
            PdiAnalytics.Percent(completed, total),
            await PdiAnalytics.AvgCompletionDaysAsync(
                plans, scoped.Select(item => item.Plan), cancellationToken),
            trends,
            managers,
            evolution.Select(item => new PdiSemesterCompetencyDto(
                item.Name, item.Objectives, item.AvgPhasePercent)).ToList(),
            byType);
    }

    /// <summary>Sem filtro = semestre corrente (jan–jun / jul–dez).</summary>
    private static (DateOnly? From, DateOnly? To) DefaultWindow(
        DateOnly? from, DateOnly? to)
    {
        if (from is not null || to is not null)
            return (from, to);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return today.Month <= 6
            ? (new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 6, 30))
            : (new DateOnly(today.Year, 7, 1), new DateOnly(today.Year, 12, 31));
    }

    /// <summary>"Nº SEMESTRE YYYY" em janela semestral; senão intervalo.</summary>
    private static string Label(DateOnly? from, DateOnly? to)
    {
        if (from is { } start && to is { } end)
        {
            if (start == new DateOnly(start.Year, 1, 1)
                && end == new DateOnly(start.Year, 6, 30))
                return $"1º SEMESTRE {start.Year}";
            if (start == new DateOnly(start.Year, 7, 1)
                && end == new DateOnly(start.Year, 12, 31))
                return $"2º SEMESTRE {start.Year}";
            return $"{start:dd/MM/yyyy} – {end:dd/MM/yyyy}";
        }

        return $"{from?.ToString("dd/MM/yyyy") ?? "início"} – " +
               $"{to?.ToString("dd/MM/yyyy") ?? "hoje"}";
    }

    /// <summary>Janela anterior do mesmo tamanho (lado fechado).</summary>
    private static PdiSemesterComparisonDto ComparePeriods(
        IReadOnlyCollection<PdiWithOwner> all,
        DateOnly? from,
        DateOnly? to)
    {
        if (from is not { } start || to is not { } end)
            return new PdiSemesterComparisonDto(0, 0, 0, 0, 0, 0);

        var length = end.DayNumber - start.DayNumber + 1;
        var previousTo = start.AddDays(-1);
        var previousFrom = start.AddDays(-length);

        var previousScoped = all
            .Where(item => PdiAnalytics.InPeriod(
                item.Plan.TargetDate, previousFrom, previousTo))
            .ToList();
        var previousCompleted = previousScoped.Count(item =>
            item.Plan.Status == PdiStatus.Completed);

        var currentCompleted = all.Count(item =>
            item.Plan.Status == PdiStatus.Completed
            && PdiAnalytics.InPeriod(item.Plan.TargetDate, from, to));

        return new PdiSemesterComparisonDto(
            previousScoped.Count,
            previousCompleted,
            PdiAnalytics.Percent(previousCompleted, previousScoped.Count),
            all.Count(item => PdiAnalytics.InPeriod(
                item.Plan.TargetDate, from, to)) - previousScoped.Count,
            currentCompleted - previousCompleted,
            PdiAnalytics.Percent(currentCompleted, all.Count(item =>
                PdiAnalytics.InPeriod(item.Plan.TargetDate, from, to)))
            - PdiAnalytics.Percent(
                previousCompleted, previousScoped.Count));
    }
}

/// <summary>§22 — relatório de dificuldades (Admin; filtro de período opcional).</summary>
public sealed record GetPdiDifficultiesReportQuery(
    DateOnly? From = null,
    DateOnly? To = null)
    : IQuery<PdiDifficultiesReportDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed class GetPdiDifficultiesReportQueryHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetPdiDifficultiesReportQuery, PdiDifficultiesReportDto>
{
    public async Task<PdiDifficultiesReportDto> Handle(
        GetPdiDifficultiesReportQuery request,
        CancellationToken cancellationToken)
    {
        PdiAnalytics.EnsureAdmin(
            currentUser, "o relatório de dificuldades do PDI");
        PdiAnalytics.EnsurePeriod(request.From, request.To);

        var scoped = (await plans.ListWithOwnersAsync(
                onlyUserId: null, cancellationToken))
            .Where(item => PdiAnalytics.InPeriod(
                item.Plan.TargetDate, request.From, request.To))
            .Select(item => item.Plan)
            .ToList();

        var impediments = new List<PdiImpediment>();
        foreach (var plan in scoped)
        {
            var rows = await repository.GetTreeAsync(
                plan.UserId, cancellationToken);
            var phaseIds = rows.Phases.Select(phase => phase.Id).ToList();
            impediments.AddRange(await repository.ListImpedimentsAsync(
                phaseIds, cancellationToken));
        }

        var (byType, resolved, avgResolutionDays) =
            PdiAnalytics.AggregateImpediments(impediments);

        return new PdiDifficultiesReportDto(
            request.From,
            request.To,
            impediments.Count,
            byType,
            resolved,
            avgResolutionDays);
    }
}

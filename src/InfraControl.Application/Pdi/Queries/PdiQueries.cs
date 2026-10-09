using InfraControl.Application.Pdi.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Pdi.Queries;

// Sem cache (IQuery direto): o PDI muda a cada mutation do próprio dono
// ou do Admin. Admin vê todos; Operador vê só o seu (lista 0..1 itens).
// Histórico (timeline, ADR-029): mesmo escopo — Operador só o próprio.

public sealed record ListPdisQuery
    : IQuery<IReadOnlyCollection<PdiDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class ListPdisQueryHandler(IPdiRepository repository, ICurrentUserContext currentUser)
    : IRequestHandler<ListPdisQuery, IReadOnlyCollection<PdiDto>>
{
    public async Task<IReadOnlyCollection<PdiDto>> Handle(
        ListPdisQuery request, CancellationToken cancellationToken)
    {
        var isAdmin = currentUser.Roles.Contains(UserRole.Admin.ToString());
        if (!isAdmin)
        {
            var ownUserId = currentUser.UserId
                ?? throw new UnauthorizedAccessException(
                    "Usuário autenticado sem identificação — não é possível listar o PDI.");

            var own = await repository.ListWithOwnersAsync(ownUserId, cancellationToken);
            return own
                .Select(item => PdiDto.From(
                    item.Plan, item.OwnerName, item.OwnerEmail, item.ManagerName))
                .ToArray();
        }

        var all = await repository.ListWithOwnersAsync(onlyUserId: null, cancellationToken);
        return all
            .Select(item => PdiDto.From(
                item.Plan, item.OwnerName, item.OwnerEmail, item.ManagerName))
            .ToArray();
    }
}

/// <summary>Timeline do PDI do usuário (docs/30 §17, ADR-029) — mais recente primeiro.</summary>
public sealed record GetPdiHistoryQuery(Guid UserId)
    : IQuery<IReadOnlyCollection<PdiHistoryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class GetPdiHistoryQueryHandler(
    IPdiRepository repository, ICurrentUserContext currentUser)
    : IRequestHandler<GetPdiHistoryQuery, IReadOnlyCollection<PdiHistoryDto>>
{
    public async Task<IReadOnlyCollection<PdiHistoryDto>> Handle(
        GetPdiHistoryQuery request, CancellationToken cancellationToken)
    {
        var isAdmin = currentUser.Roles.Contains(UserRole.Admin.ToString());
        if (!isAdmin && currentUser.UserId != request.UserId)
            throw new UnauthorizedAccessException(
                "Você só pode ver o histórico do seu próprio PDI.");

        var rows = await repository.ListHistoryAsync(request.UserId, cancellationToken);
        return rows
            .Select(row => PdiHistoryDto.From(row.Entry, row.ActorName))
            .ToArray();
    }
}

// ------------------------------------------------------------------
// Avaliação final (docs/30 §15.3/§16 — Fase 6, ADR-036)
// ------------------------------------------------------------------

/// <summary>Avaliação final do PDI: plano + autoavaliação + avaliação do gestor + resultado (§16).</summary>
public sealed record GetPdiEvaluationQuery(Guid UserId)
    : IQuery<PdiEvaluationDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class GetPdiEvaluationQueryHandler(
    IPdiRepository plans,
    IPdiStructureRepository repository,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetPdiEvaluationQuery, PdiEvaluationDto>
{
    public async Task<PdiEvaluationDto> Handle(
        GetPdiEvaluationQuery request, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"PDI do usuário '{request.UserId}' não encontrado.");

        PdiAccess.EnsureCanView(currentUser, plan);

        return await PdiEvaluationBuilder.BuildAsync(
            repository, plans, plan, cancellationToken);
    }
}

/// <summary>
/// Monta a avaliação final do plano (leitura pura, sem guard de
/// acesso) — usado pelo GET evaluation e pelo relatório individual
/// (§22 — Fase 8).
/// </summary>
internal static class PdiEvaluationBuilder
{
    internal static async Task<PdiEvaluationDto> BuildAsync(
        IPdiStructureRepository repository,
        IPdiRepository plans,
        IndividualDevelopmentPlan plan,
        CancellationToken cancellationToken)
    {
        var self = await repository.GetAssessmentAsync(
            plan.Id, PdiAssessmentSide.Self, cancellationToken);
        var manager = await repository.GetAssessmentAsync(
            plan.Id, PdiAssessmentSide.Manager, cancellationToken);
        var result = await repository.GetResultAsync(
            plan.Id, cancellationToken);

        string? managerName = plan.ManagerId is { } managerId
            ? await plans.GetUserNameAsync(managerId, cancellationToken)
            : null;
        string? selfName = self is null
            ? null
            : await plans.GetUserNameAsync(self.AuthorId, cancellationToken);
        string? managerAuthorName = manager is null
            ? null
            : await plans.GetUserNameAsync(manager.AuthorId, cancellationToken);
        string? finalizedName = result is null
            ? null
            : await plans.GetUserNameAsync(result.FinalizedById, cancellationToken);

        return new PdiEvaluationDto(
            plan.UserId,
            plan.Status.ToString(),
            plan.Strategy,
            plan.TargetDate,
            plan.ManagerId,
            managerName,
            plan.PeriodStart,
            plan.PeriodEnd,
            self is null ? null : PdiAssessmentDto.From(self, plan.UserId, selfName),
            manager is null
                ? null
                : PdiAssessmentDto.From(manager, plan.UserId, managerAuthorName),
            result is null
                ? null
                : PdiResultDto.From(result, plan.UserId, finalizedName));
    }
}

// ------------------------------------------------------------------
// Dashboards (Fase 7 — docs/30 §18, §19, §20, ADR-037)
// ------------------------------------------------------------------

/// <summary>
/// §18 — dashboard do colaborador: progresso, fases, próximo prazo,
/// pendências e totais do acompanhamento. Leitura = Admin/dono/gestor
/// (mesmo guard do acompanhamento/avaliação).
/// </summary>
public sealed record GetPdiDashboardQuery(Guid UserId)
    : IQuery<PdiDashboardDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class GetPdiDashboardQueryHandler(
    IPdiRepository plans,
    IPdiStructureRepository repository,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetPdiDashboardQuery, PdiDashboardDto>
{
    public async Task<PdiDashboardDto> Handle(
        GetPdiDashboardQuery request, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"PDI do usuário '{request.UserId}' não encontrado.");
        PdiAccess.EnsureCanView(currentUser, plan);

        var rows = await repository.GetTreeAsync(request.UserId, cancellationToken);
        var phases = rows.Phases;
        var phaseIds = phases.Select(phase => phase.Id).ToList();

        var checkIns = await repository.ListCheckInsAsync(phaseIds, cancellationToken);
        var impediments = await repository.ListImpedimentsAsync(phaseIds, cancellationToken);
        var evidences = await repository.ListEvidenceAsync(phaseIds, cancellationToken);

        var progress = phases.Count == 0
            ? 0
            : (int)Math.Round(
                phases.Average(phase => (double)phase.Percent),
                MidpointRounding.AwayFromZero);

        var nextDueDate = phases
            .Where(phase => phase.Status != PdiPhaseStatus.Completed
                && phase.EndDate is not null)
            .Select(phase => phase.EndDate)
            .Min();

        return new PdiDashboardDto(
            plan.UserId,
            plan.Status.ToString(),
            progress,
            phases
                .Select(phase => new PdiDashboardPhaseDto(
                    phase.Id, phase.Title, phase.Status.ToString()))
                .ToArray(),
            nextDueDate,
            impediments.Count(impediment =>
                impediment.Status == PdiImpedimentStatus.Open),
            evidences.Count,
            checkIns.Count);
    }
}

/// <summary>
/// §19 — dashboard do gestor: planos em que sou gestor, classificados
/// como atrasado / atenção / dentro do prazo, com agregados de fases a
/// vencer (7 dias), impedimentos abertos e evidências pendentes.
/// </summary>
public sealed record GetPdiManagerDashboardQuery
    : IQuery<PdiManagerDashboardDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class GetPdiManagerDashboardQueryHandler(
    IPdiRepository plans,
    IPdiStructureRepository repository,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetPdiManagerDashboardQuery, PdiManagerDashboardDto>
{
    public async Task<PdiManagerDashboardDto> Handle(
        GetPdiManagerDashboardQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(
                "Usuário autenticado sem identificação — não é possível montar o dashboard do gestor.");

        var managed = await plans.ListByManagerAsync(userId, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dueHorizon = today.AddDays(7);

        var entries = new List<PdiManagerPlanDto>();
        var phasesDueSoon = 0;
        var openImpediments = 0;
        var pendingEvidences = 0;

        foreach (var item in managed)
        {
            var plan = item.Plan;
            var finished = plan.Status is PdiStatus.Completed or PdiStatus.Cancelled;
            var overdue = !finished && plan.TargetDate < today;

            var rows = await repository.GetTreeAsync(
                plan.UserId, cancellationToken);
            var phaseIds = rows.Phases.Select(phase => phase.Id).ToList();

            var impediments = await repository.ListImpedimentsAsync(
                phaseIds, cancellationToken);
            var evidences = await repository.ListEvidenceAsync(
                phaseIds, cancellationToken);

            var hasOpenImpediment = impediments.Any(impediment =>
                impediment.Status == PdiImpedimentStatus.Open);
            var hasPendingEvidence = evidences.Any(evidence =>
                evidence.Status == PdiEvidenceStatus.Pending);
            var hasPendingValidation = rows.Phases.Any(phase =>
                phase.Status == PdiPhaseStatus.PendingValidation);

            var needsAttention = !overdue && !finished
                && (hasOpenImpediment || hasPendingEvidence || hasPendingValidation);

            entries.Add(new PdiManagerPlanDto(
                plan.UserId, item.OwnerName, plan.Status.ToString(),
                plan.TargetDate, overdue, needsAttention));

            phasesDueSoon += rows.Phases.Count(phase =>
                phase.Status != PdiPhaseStatus.Completed
                && phase.EndDate is { } endDate
                && endDate >= today && endDate <= dueHorizon);
            openImpediments += impediments.Count(impediment =>
                impediment.Status == PdiImpedimentStatus.Open);
            pendingEvidences += evidences.Count(evidence =>
                evidence.Status == PdiEvidenceStatus.Pending);
        }

        return new PdiManagerDashboardDto(
            entries.Count,
            entries.Count(entry => !entry.Overdue && !entry.NeedsAttention),
            entries.Count(entry => entry.NeedsAttention),
            entries.Count(entry => entry.Overdue),
            phasesDueSoon,
            openImpediments,
            pendingEvidences,
            entries.ToArray());
    }
}

/// <summary>
/// §20 — dashboard administrativo: contagens globais por status de
/// plano, taxa de conclusão e agregados de fases/check-ins/evidências/
/// impedamentos (somente Admin — guard também na policy da API).
/// </summary>
public sealed record GetPdiAdminDashboardQuery(
    DateOnly? From = null,
    DateOnly? To = null)
    : IQuery<PdiAdminDashboardDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed class GetPdiAdminDashboardQueryHandler(
    IPdiRepository plans,
    IPdiStructureRepository repository,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetPdiAdminDashboardQuery, PdiAdminDashboardDto>
{
    public async Task<PdiAdminDashboardDto> Handle(
        GetPdiAdminDashboardQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains(UserRole.Admin.ToString()))
            throw new UnauthorizedAccessException(
                "Apenas administradores podem ver o dashboard administrativo do PDI.");

        PdiAnalytics.EnsurePeriod(request.From, request.To);

        var all = (await plans.ListWithOwnersAsync(
                onlyUserId: null, cancellationToken))
            .Where(item => PdiAnalytics.InPeriod(
                item.Plan.TargetDate, request.From, request.To))
            .ToList();

        var total = all.Count;
        var completed = all.Count(item =>
            item.Plan.Status == PdiStatus.Completed);
        var replanned = all.Count(item =>
            item.Plan.Status == PdiStatus.Replanned);
        var cancelled = all.Count(item =>
            item.Plan.Status == PdiStatus.Cancelled);
        var inProgress = total - completed - replanned - cancelled;

        var completedPhases = 0;
        var totalCheckIns = 0;
        var totalEvidences = 0;
        var totalImpediments = 0;

        foreach (var item in all)
        {
            var rows = await repository.GetTreeAsync(
                item.Plan.UserId, cancellationToken);
            var phaseIds = rows.Phases.Select(phase => phase.Id).ToList();

            completedPhases += rows.Phases.Count(phase =>
                phase.Status == PdiPhaseStatus.Completed);
            totalCheckIns += (await repository.ListCheckInsAsync(
                phaseIds, cancellationToken)).Count;
            totalEvidences += (await repository.ListEvidenceAsync(
                phaseIds, cancellationToken)).Count;
            totalImpediments += (await repository.ListImpedimentsAsync(
                phaseIds, cancellationToken)).Count;
        }

        var rate = total == 0
            ? 0
            : (int)Math.Round(
                100.0 * completed / total, MidpointRounding.AwayFromZero);

        return new PdiAdminDashboardDto(
            total, completed, inProgress, replanned, cancelled, rate,
            completedPhases, totalCheckIns, totalEvidences, totalImpediments);
    }
}

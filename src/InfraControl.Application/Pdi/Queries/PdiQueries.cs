using InfraControl.Application.Pdi.Ports;
using InfraControl.BuildingBlocks.Application;
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

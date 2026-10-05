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

using InfraControl.Application.Pdi.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Pdi.Queries;

// Sem cache (IQuery direto): o PDI muda a cada mutation do próprio dono
// ou do Admin. Admin vê todos; Operador vê só o seu (lista 0..1 itens).

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
                .Select(item => PdiDto.From(item.Plan, item.OwnerName, item.OwnerEmail))
                .ToArray();
        }

        var all = await repository.ListWithOwnersAsync(onlyUserId: null, cancellationToken);
        return all
            .Select(item => PdiDto.From(item.Plan, item.OwnerName, item.OwnerEmail))
            .ToArray();
    }
}

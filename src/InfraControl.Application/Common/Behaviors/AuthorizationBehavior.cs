using InfraControl.BuildingBlocks.Application;
using MediatR;

namespace InfraControl.Application.Common.Behaviors;

public sealed class AuthorizationBehavior<TRequest, TResponse>(ICurrentUserContext currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IAuthorizedRequest authorizedRequest)
            return next();

        // Toda request autorizada exige usuário autenticado, mesmo sem role
        // declarada (leitura): sem isso a camada MediatR aceitaria chamadas
        // anônimas de dentro de workers ou chamadores que não passam pelo HTTP.
        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Autenticação é necessária para esta requisição.");

        if (authorizedRequest.RequiredRoles.Count == 0)
            return next();

        var grantedRoles = currentUser.Roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requiredRoleNames = authorizedRequest.RequiredRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Nível de acesso (RoleAccess.Read/Operate/Administer): basta UMA
        // role exigida estar concedida (OR) — Read = qualquer role, Operate
        // = Operator|Admin, Administer = Admin. AND tornaria Read inatingível.
        // ordenação determinística: a mensagem é observável (401) e testada
        if (!grantedRoles.Overlaps(requiredRoleNames))
            throw new UnauthorizedAccessException(
                $"Roles ausentes: {string.Join(", ", requiredRoleNames.OrderBy(role => role, StringComparer.Ordinal))}");

        return next();
    }
}

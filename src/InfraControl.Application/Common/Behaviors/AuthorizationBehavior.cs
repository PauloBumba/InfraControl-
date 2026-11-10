using InfraControl.Application.Common.Security;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Access;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Common.Behaviors;

public sealed class AuthorizationBehavior<TRequest, TResponse>(
    ICurrentUserContext currentUser,
    IAccessPolicyService accessPolicy)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IAuthorizedRequest authorizedRequest)
            return await next();

        // Toda request autorizada exige usuário autenticado, mesmo sem role
        // declarada (leitura): sem isso a camada MediatR aceitaria chamadas
        // anônimas de dentro de workers ou chamadores que não passam pelo HTTP.
        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Autenticação é necessária para esta requisição.");

        if (authorizedRequest.RequiredRoles.Count == 0)
            return await next();

        // ADR-066: módulo resolvido pelo namespace → nível exigido derivado
        // de RequiredRoles → matriz role×módulo. Sem módulo catalogado ou
        // RequiredRoles fora dos 3 conjuntos canônicos → fallback legado.
        var moduleKey = AccessModuleCatalog.FromRequestNamespace(
            request.GetType().Namespace);
        var requiredLevel = CanonicalLevel(authorizedRequest.RequiredRoles);

        if (moduleKey is not null && requiredLevel is not null)
        {
            var allowed = await accessPolicy.HasAccessAsync(
                currentUser.Roles, requiredLevel.Value, moduleKey, cancellationToken);
            if (!allowed)
                throw new UnauthorizedAccessException(
                    $"Acesso negado ao módulo '{moduleKey}' (nível {requiredLevel}).");
            return await next();
        }

        var grantedRoles = currentUser.Roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requiredRoleNames = authorizedRequest.RequiredRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Nível de acesso (RoleAccess.Read/Operate/Administer): basta UMA
        // role exigida estar concedida (OR) — Read = qualquer role, Operate
        // = Operator|Admin, Administer = Admin. AND tornaria Read inatingível.
        // ordenação determinística: a mensagem é observável (401) e testada
        if (!grantedRoles.Overlaps(requiredRoleNames))
            throw new UnauthorizedAccessException(
                $"Roles ausentes: {string.Join(", ", requiredRoleNames.OrderBy(role => role, StringComparer.Ordinal))}");

        return await next();
    }

    /// <summary>
    /// Converte RequiredRoles no nível canônico somente quando o conjunto é
    /// exatamente um dos 3 padrões do ADR-016; caso contrário null → fallback.
    /// </summary>
    internal static AccessLevel? CanonicalLevel(IReadOnlyCollection<string> required)
    {
        var set = required.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (set.Count == 1 && set.Contains(UserRole.Admin.ToString()))
            return AccessLevel.Administer;
        if (set.Count == 2 &&
            set.Contains(UserRole.Operator.ToString()) &&
            set.Contains(UserRole.Admin.ToString()))
            return AccessLevel.Operate;
        if (set.Count == 3)
            return AccessLevel.Read;
        return null;
    }
}

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
        if (request is not IAuthorizedRequest authorizedRequest || authorizedRequest.RequiredPermissions.Count == 0)
            return next();

        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Authentication is required for this request.");

        var granted = currentUser.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = authorizedRequest.RequiredPermissions
            .Where(permission => !granted.Contains(permission))
            .ToArray();
        if (missing.Length > 0)
            throw new UnauthorizedAccessException($"Missing permissions: {string.Join(", ", missing)}");

        return next();
    }
}

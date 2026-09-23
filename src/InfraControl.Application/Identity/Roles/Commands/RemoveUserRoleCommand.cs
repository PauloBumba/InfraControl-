using InfraControl.Domain.Enums;
using InfraControl.Application.Identity.Roles.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Identity.Roles.Commands;

public sealed record RemoveUserRoleCommand(Guid UserId, UserRole Role)
    : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.IdentityRolesManage.ToString()];
}

public sealed class RemoveUserRoleCommandHandler(
    IUserRoleRepository repository,
    ILogger<RemoveUserRoleCommandHandler> logger) : IRequestHandler<RemoveUserRoleCommand>
{
    public async Task Handle(RemoveUserRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await repository.GetAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException($"User '{request.UserId}' was not found.");

        if (!user.Roles.Contains(request.Role)) return;

        if (request.Role == UserRole.Admin && user.IsActive &&
            await repository.CountActiveAdminsAsync(cancellationToken) <= 1)
        {
            throw new InvalidOperationException("The last active administrator cannot lose the Admin role.");
        }

        user.RemoveRole(request.Role);
        await repository.SaveRoleChangeAsync(user, request.Role, assign: false, cancellationToken);
        logger.LogInformation("Role {Role} removed from user {UserId}.", request.Role, request.UserId);
    }
}

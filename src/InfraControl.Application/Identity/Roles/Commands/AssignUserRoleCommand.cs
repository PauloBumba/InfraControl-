using InfraControl.Domain.Enums;
using InfraControl.Application.Identity.Roles.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Identity.Roles.Commands;

public sealed record AssignUserRoleCommand(Guid UserId, UserRole Role)
    : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.IdentityRolesManage.ToString()];
}

public sealed class AssignUserRoleCommandHandler(
    IUserRoleRepository repository,
    ILogger<AssignUserRoleCommandHandler> logger) : IRequestHandler<AssignUserRoleCommand>
{
    public async Task Handle(AssignUserRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await repository.GetAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException($"User '{request.UserId}' was not found.");

        if (user.Roles.Contains(request.Role)) return;

        user.AssignRole(request.Role);
        await repository.SaveRoleChangeAsync(user, request.Role, assign: true, cancellationToken);
        logger.LogInformation("Role {Role} assigned to user {UserId}.", request.Role, request.UserId);
    }
}

using InfraControl.Domain.Enums;
using InfraControl.Application.Identity.Roles.Ports;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Identity.Roles.Commands;

public sealed record AssignUserRoleCommand(Guid UserId, UserRole Role) : IRequest;

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

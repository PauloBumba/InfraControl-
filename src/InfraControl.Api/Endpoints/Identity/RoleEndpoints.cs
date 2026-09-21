using InfraControl.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using InfraControl.Application.Identity.Roles.Commands;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Endpoints.Identity;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/identity/users")]
public sealed class RoleEndpoints(ISender sender) : ControllerBase
{
    [HttpPut("{userId:guid}/roles")]
    public async Task<IActionResult> Assign(Guid userId, [FromBody] RoleRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new AssignUserRoleCommand(userId, request.Role), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{userId:guid}/roles/{role}")]
    public async Task<IActionResult> Remove(Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        await sender.Send(new RemoveUserRoleCommand(userId, role), cancellationToken);
        return NoContent();
    }
}

public sealed record RoleRequest([property: EnumDataType(typeof(UserRole))] UserRole Role);

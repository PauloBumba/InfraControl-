using System.ComponentModel.DataAnnotations;
using InfraControl.Application.Identity;
using InfraControl.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Endpoints.Identity;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/identity/users")]
public sealed class RoleEndpoints(IRoleManagementService roleManagement) : ControllerBase
{
    [HttpPut("{userId:guid}/roles")]
    public async Task<IActionResult> Assign(Guid userId, [FromBody] RoleRequest request, CancellationToken cancellationToken)
    {
        await roleManagement.AssignAsync(userId, request.Role, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{userId:guid}/roles/{role}")]
    public async Task<IActionResult> Remove(Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        await roleManagement.RemoveAsync(userId, role, cancellationToken);
        return NoContent();
    }
}

public sealed record RoleRequest([property: EnumDataType(typeof(UserRole))] UserRole Role);

using InfraControl.Domain.Enums;
using InfraControl.Application.Identity.Roles.Commands;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Admin;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Admin;

[Authorize(Policy = "AdminOnly")]
[Route("api/identity/users")]
public sealed class RolesController(ISender sender) : BaseController
{
    [HttpPut("{userId:guid}/roles")]
    public async Task<IActionResult> Assign(Guid userId, [FromBody] RoleViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new AssignUserRoleCommand(userId, model.Role), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Fail([exception.Message]));
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail([exception.Message]));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(ApiResponse<object>.Fail([exception.Message]));
        }
    }

    [HttpDelete("{userId:guid}/roles/{role}")]
    public async Task<IActionResult> Remove(Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new RemoveUserRoleCommand(userId, role), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Fail([exception.Message]));
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail([exception.Message]));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(ApiResponse<object>.Fail([exception.Message]));
        }
    }
}

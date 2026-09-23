using InfraControl.Application.Identity.Administration;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Admin;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Admin;

[Route("api/v1/admin/users")]
[Authorize(Policy = "IdentityUsersRead")]
public sealed class UsersController(IIdentityAdministrationService identityService) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await identityService.ListUsersAsync(cancellationToken)));

    [HttpPatch("{userId:guid}/status")]
    [Authorize(Policy = "IdentityUsersManage")]
    public async Task<IActionResult> SetStatus(Guid userId, SetUserStatusViewModel model, CancellationToken cancellationToken)
        => await identityService.SetActiveAsync(userId, model.Active, cancellationToken)
            ? NoContent()
            : NotFound(ApiResponse<object>.Fail(["User not found."]));

    [HttpPost("{userId:guid}/roles/{role}")]
    [Authorize(Policy = "IdentityUsersManage")]
    public async Task<IActionResult> AssignRole(Guid userId, UserRole role, CancellationToken cancellationToken)
        => await identityService.AssignRoleAsync(userId, role, cancellationToken)
            ? NoContent()
            : NotFound(ApiResponse<object>.Fail(["User not found."]));

    [HttpDelete("{userId:guid}/roles/{role}")]
    [Authorize(Policy = "IdentityUsersManage")]
    public async Task<IActionResult> RemoveRole(Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        try
        {
            return await identityService.RemoveRoleAsync(userId, role, cancellationToken)
                ? NoContent()
                : NotFound(ApiResponse<object>.Fail(["User not found."]));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(ApiResponse<object>.Fail([exception.Message]));
        }
    }
}

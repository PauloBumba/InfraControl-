using InfraControl.Application.Identity.Administration;
using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = "IdentityUsersRead")]
public sealed class UsersController(IIdentityAdministrationService identityService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(await identityService.ListUsersAsync(cancellationToken));

    [HttpPatch("{userId:guid}/status")]
    [Authorize(Policy = "IdentityUsersManage")]
    public async Task<IActionResult> SetStatus(Guid userId, SetUserStatusRequest request, CancellationToken cancellationToken)
        => await identityService.SetActiveAsync(userId, request.Active, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("{userId:guid}/roles/{role}")]
    [Authorize(Policy = "IdentityUsersManage")]
    public async Task<IActionResult> AssignRole(Guid userId, UserRole role, CancellationToken cancellationToken)
        => await identityService.AssignRoleAsync(userId, role, cancellationToken) ? NoContent() : NotFound();

    [HttpDelete("{userId:guid}/roles/{role}")]
    [Authorize(Policy = "IdentityUsersManage")]
    public async Task<IActionResult> RemoveRole(Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        try
        {
            return await identityService.RemoveRoleAsync(userId, role, cancellationToken) ? NoContent() : NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    public sealed record SetUserStatusRequest(bool Active);
}

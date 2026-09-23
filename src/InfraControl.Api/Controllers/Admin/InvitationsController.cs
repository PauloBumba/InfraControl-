using InfraControl.Application.Identity.Invitations.Commands;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Admin;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Admin;

[Authorize(Policy = "IdentityUsersManage")]
[Route("api/v1/admin/invitations")]
public sealed class InvitationsController(ISender sender) : BaseController
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvitationViewModel model, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirst("sub")?.Value ?? User.FindFirst("nameidentifier")?.Value,
                out var inviterId))
            return Unauthorized(ApiResponse<object>.Fail(["An authenticated inviter id is required."]));

        try
        {
            var result = await sender.Send(
                new CreateInvitationCommand(model.Email, model.Roles, inviterId, model.ExpiresInDays),
                cancellationToken);

            return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ApiResponse<object>.Fail([exception.Message]));
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

    [HttpDelete("{invitationId:guid}")]
    public async Task<IActionResult> Cancel(Guid invitationId, CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new CancelInvitationCommand(invitationId), cancellationToken);
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

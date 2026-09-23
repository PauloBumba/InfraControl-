using InfraControl.Application.Identity.Invitations.Commands;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Identity;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Identity;

[Route("api/v1/auth/invitations")]
public sealed class InvitationAcceptanceController(ISender sender) : BaseController
{
    [AllowAnonymous]
    [HttpPost("{invitationId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid invitationId, [FromBody] AcceptInvitationViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(new AcceptInvitationCommand(invitationId, model.Token), cancellationToken);
            return Ok(ApiResponse.Ok(result));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(["Invitation not found."]));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<object>.Fail(["Invalid invitation token."]));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ApiResponse<object>.Fail([exception.Message]));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(ApiResponse<object>.Fail([exception.Message]));
        }
    }

    [AllowAnonymous]
    [HttpPost("{invitationId:guid}/set-password")]
    public async Task<IActionResult> SetPassword(
        Guid invitationId,
        [FromBody] SetInvitationPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new SetInvitationPasswordCommand(invitationId, model.Token, model.Password),
                cancellationToken);
            return Ok(ApiResponse.Ok(result));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(["Invitation not found."]));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<object>.Fail(["Invalid invitation token."]));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ApiResponse<object>.Fail([exception.Message]));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(ApiResponse<object>.Fail([exception.Message]));
        }
    }

    [AllowAnonymous]
    [HttpPost("{invitationId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid invitationId, [FromBody] AcceptInvitationViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new RejectInvitationCommand(invitationId, model.Token), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(["Invitation not found."]));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<object>.Fail(["Invalid invitation token."]));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ApiResponse<object>.Fail([exception.Message]));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(ApiResponse<object>.Fail([exception.Message]));
        }
    }
}

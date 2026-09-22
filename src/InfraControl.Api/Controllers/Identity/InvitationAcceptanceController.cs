using InfraControl.Application.Identity.Invitations.Commands;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Identity;
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
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Invitation not found." });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Invalid invitation token." });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}

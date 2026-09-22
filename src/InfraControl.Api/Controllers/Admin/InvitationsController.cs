using InfraControl.Application.Identity.Invitations.Commands;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Admin;
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
            return Unauthorized();

        try
        {
            var result = await sender.Send(
                new CreateInvitationCommand(model.Email, model.Roles, inviterId, model.ExpiresInDays),
                cancellationToken);

            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
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
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }
}

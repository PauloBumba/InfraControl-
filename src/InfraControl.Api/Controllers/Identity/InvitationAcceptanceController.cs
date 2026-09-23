using InfraControl.Application.Identity.Invitations.Commands;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Identity;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Identity;

/// <summary>
/// F0-T023: falhas (convite inexistente, token inválido, estado inválido,
/// validação) são embrulhadas pelo GlobalExceptionHandler com status correto.
/// </summary>
[Route("api/v1/auth/invitations")]
public sealed class InvitationAcceptanceController(ISender sender) : BaseController
{
    [AllowAnonymous]
    [HttpPost("{invitationId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid invitationId, [FromBody] AcceptInvitationViewModel model, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AcceptInvitationCommand(invitationId, model.Token), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [AllowAnonymous]
    [HttpPost("{invitationId:guid}/set-password")]
    public async Task<IActionResult> SetPassword(
        Guid invitationId,
        [FromBody] SetInvitationPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SetInvitationPasswordCommand(invitationId, model.Token, model.Password),
            cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [AllowAnonymous]
    [HttpPost("{invitationId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid invitationId, [FromBody] AcceptInvitationViewModel model, CancellationToken cancellationToken)
    {
        await sender.Send(new RejectInvitationCommand(invitationId, model.Token), cancellationToken);
        return NoContent();
    }
}

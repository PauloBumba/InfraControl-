using InfraControl.Application.Identity.Invitations.Commands;
using InfraControl.Application.Identity.Invitations.Queries;
using InfraControl.Api.Controllers;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers;

/// <summary>
/// Endpoints de convites em tempo real dentro da plataforma
/// (usuário já autenticado). Não exige admin, apenas autenticação.
/// </summary>
[Authorize]
[Route("api/v1/invitations")]
public sealed class InvitationsController(ISender sender) : BaseController
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMyInvitations(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyInvitationsQuery(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("{invitationId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid invitationId, CancellationToken cancellationToken)
    {
        await sender.Send(new AcceptInvitationInternalCommand(invitationId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{invitationId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid invitationId, CancellationToken cancellationToken)
    {
        await sender.Send(new RejectInvitationInternalCommand(invitationId), cancellationToken);
        return NoContent();
    }
}

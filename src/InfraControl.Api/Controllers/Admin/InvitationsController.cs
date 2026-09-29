using InfraControl.Application.Identity.Invitations.Commands;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Admin;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Admin;

/// <summary>
/// F0-T023: falhas (KeyNotFound/UnauthorizedAccess/InvalidOperation/Argument)
/// são embrulhadas pelo GlobalExceptionHandler — aqui só o sucesso compõe corpo.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.Administer)]
[Route("api/v1/admin/invitations")]
public sealed class InvitationsController(ISender sender) : BaseController
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvitationViewModel model, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirst("sub")?.Value ?? User.FindFirst("nameidentifier")?.Value,
                out var inviterId))
            return Unauthorized(ApiResponse<object>.Fail(["É necessário um convidante autenticado."]));

        // F1-T017: origem do link do convite — mesma derivação do forgot-password.
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = await sender.Send(
            new CreateInvitationCommand(model.Email, model.Roles, inviterId, model.ExpiresInDays, baseUrl),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpDelete("{invitationId:guid}")]
    public async Task<IActionResult> Cancel(Guid invitationId, CancellationToken cancellationToken)
    {
        await sender.Send(new CancelInvitationCommand(invitationId), cancellationToken);
        return NoContent();
    }
}

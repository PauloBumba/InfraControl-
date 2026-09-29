using InfraControl.Application.Network.SwitchPorts.Commands;
using InfraControl.Application.Network.SwitchPorts.Queries;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Network;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Network;

/// <summary>
/// F3-T002: portas dos switches. Coleção sob o switch
/// (network/switches/{id}/ports), item em network/switch-ports/{id}.
/// Guard de type=Switch é reusado do F3-T001 no handler (404 pt-BR);
/// falhas embrulhadas pelo GlobalExceptionHandler.
/// </summary>
[Route("api/v1/network")]
public sealed class SwitchPortsController(ISender sender) : BaseController
{
    [HttpGet("switches/{switchId:guid}/ports")]
    [Authorize]
    public async Task<IActionResult> Search(
        Guid switchId,
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchSwitchPortsQuery(switchId, term, page, pageSize), cancellationToken)));

    [HttpPost("switches/{switchId:guid}/ports")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Create(
        Guid switchId,
        [FromBody] CreateSwitchPortViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateSwitchPortCommand(switchId, model.Name, model.Description),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpGet("switch-ports/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetSwitchPortQuery(id), cancellationToken)));

    [HttpPut("switch-ports/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateSwitchPortViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateSwitchPortCommand(id, model.Name, model.Description, model.LinkUp),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("switch-ports/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteSwitchPortCommand(id), cancellationToken);
        return NoContent();
    }
}

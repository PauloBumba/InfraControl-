using InfraControl.Application.Network.Vlans.Commands;
using InfraControl.Application.Network.Vlans.Queries;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Network;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Network;

/// <summary>
/// F3-T003: cadastro de VLANs (tag 802.1Q + nome únicos). A vinculação
/// N:N com portas (SwitchPortVlan) é F3-T004. Falhas embrulhadas pelo
/// GlobalExceptionHandler (400/404/409 pt-BR).
/// </summary>
[Route("api/v1/network")]
public sealed class VlansController(ISender sender) : BaseController
{
    [HttpGet("vlans")]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchVlansQuery(term, page, pageSize), cancellationToken)));

    [HttpPost("vlans")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Create(
        [FromBody] CreateVlanViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateVlanCommand(model.Name, model.VlanNumber, model.Description),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpGet("vlans/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetVlanQuery(id), cancellationToken)));

    [HttpPut("vlans/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateVlanViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateVlanCommand(id, model.Name, model.VlanNumber, model.Description),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("vlans/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteVlanCommand(id), cancellationToken);
        return NoContent();
    }
}

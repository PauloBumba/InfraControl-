using InfraControl.Application.Network.SwitchPortVlans.Commands;
using InfraControl.Application.Network.SwitchPortVlans.Queries;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Network;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Network;

/// <summary>
/// F3-T004: junção N:N SwitchPort ↔ Vlan (docs/03). Coleção de VLANs sob a
/// porta (switch-ports/{id}/vlans), lista de portas sob a VLAN
/// (vlans/{id}/ports). Guards 404 reusados dos slices F3-T002/F3-T003;
/// falhas embrulhadas pelo GlobalExceptionHandler (400/404/409 pt-BR).
/// </summary>
[Route("api/v1/network")]
public sealed class SwitchPortVlansController(ISender sender) : BaseController
{
    [HttpGet("switch-ports/{switchPortId:guid}/vlans")]
    [Authorize]
    public async Task<IActionResult> GetVlansOfPort(
        Guid switchPortId,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetSwitchPortVlansQuery(switchPortId), cancellationToken)));

    [HttpPost("switch-ports/{switchPortId:guid}/vlans")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Assign(
        Guid switchPortId,
        [FromBody] AssignSwitchPortVlanViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AssignVlanToSwitchPortCommand(switchPortId, model.VlanId),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpDelete("switch-ports/{switchPortId:guid}/vlans/{vlanId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Unassign(
        Guid switchPortId,
        Guid vlanId,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UnassignVlanFromSwitchPortCommand(switchPortId, vlanId),
            cancellationToken);
        return NoContent();
    }

    [HttpGet("switch-port-vlans/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetSwitchPortVlanQuery(id), cancellationToken)));

    [HttpGet("vlans/{vlanId:guid}/ports")]
    [Authorize]
    public async Task<IActionResult> GetPortsOfVlan(
        Guid vlanId,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetVlanSwitchPortsQuery(vlanId), cancellationToken)));
}

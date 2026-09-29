using InfraControl.Application.Inventory.NetworkInterfaces.Commands;
using InfraControl.Application.Inventory.NetworkInterfaces.Queries;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Inventory;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Inventory;

/// <summary>
/// F2-T004: CRUD de interfaces de rede. Falhas (400/404/409/401/403) são
/// embrulhadas pelo GlobalExceptionHandler — o controller só compõe sucesso.
/// </summary>
[Route("api/v1/inventory/network-interfaces")]
public sealed class NetworkInterfacesController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? deviceId,
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchNetworkInterfacesQuery(deviceId, term, page, pageSize), cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetNetworkInterfaceQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Create(
        [FromBody] CreateNetworkInterfaceViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateNetworkInterfaceCommand(model.DeviceId, model.Name, model.Description),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateNetworkInterfaceViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateNetworkInterfaceCommand(id, model.Name, model.Description),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteNetworkInterfaceCommand(id), cancellationToken);
        return NoContent();
    }
}

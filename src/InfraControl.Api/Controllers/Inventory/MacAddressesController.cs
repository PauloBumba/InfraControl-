using InfraControl.Application.Inventory.MacAddresses.Commands;
using InfraControl.Application.Inventory.MacAddresses.Queries;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Inventory;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Inventory;

/// <summary>
/// F2-T005: CRUD de MACs (unicidade global). Falhas (400/404/409/401/403) são
/// embrulhadas pelo GlobalExceptionHandler — o controller só compõe sucesso.
/// </summary>
[Route("api/v1/inventory/mac-addresses")]
public sealed class MacAddressesController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? interfaceId,
        [FromQuery] Guid? deviceId,
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchMacAddressesQuery(interfaceId, deviceId, term, page, pageSize), cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetMacAddressQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Create(
        [FromBody] CreateMacAddressViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateMacAddressCommand(model.InterfaceId, model.Mac),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateMacAddressViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateMacAddressCommand(id, model.Mac), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteMacAddressCommand(id), cancellationToken);
        return NoContent();
    }
}

using InfraControl.Application.Inventory.IpAddresses.Commands;
using InfraControl.Application.Inventory.IpAddresses.Queries;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Inventory;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Inventory;

/// <summary>
/// F2-T006: CRUD de IPs (unicidade global, normalização canônica). Falhas
/// (400/404/409/401/403) são embrulhadas pelo GlobalExceptionHandler — o
/// controller só compõe sucesso.
/// </summary>
[Route("api/v1/inventory/ip-addresses")]
public sealed class IpAddressesController(ISender sender) : BaseController
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
            new SearchIpAddressesQuery(interfaceId, deviceId, term, page, pageSize), cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetIpAddressQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Create(
        [FromBody] CreateIpAddressViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateIpAddressCommand(model.InterfaceId, model.Ip),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateIpAddressViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateIpAddressCommand(id, model.Ip), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteIpAddressCommand(id), cancellationToken);
        return NoContent();
    }
}

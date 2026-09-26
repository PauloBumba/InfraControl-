using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Network;
using InfraControl.Application.Network.Connections.Commands;
using InfraControl.Application.Network.Connections.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Network;

/// <summary>
/// F3-T005: Connection dirigida entre interfaces (docs/03 — Interface
/// N:N Interface via Connection). Par (origem, destino) único; self-loop
/// 400; duplicado 409. Falhas embrulhadas pelo GlobalExceptionHandler.
/// </summary>
[Route("api/v1/network/connections")]
public sealed class ConnectionsController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "InventoryRead")]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? deviceId,
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchConnectionsQuery(deviceId, term, page, pageSize),
            cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "InventoryRead")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetConnectionQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = "InventoryManage")]
    public async Task<IActionResult> Create(
        [FromBody] CreateConnectionViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateConnectionCommand(model.SourceInterfaceId, model.TargetInterfaceId),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "InventoryManage")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteConnectionCommand(id), cancellationToken);
        return NoContent();
    }
}

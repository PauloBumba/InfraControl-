using InfraControl.Application.Inventory.Racks.Commands;
using InfraControl.Application.Inventory.Racks.Queries;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Inventory;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Inventory;

/// <summary>
/// F2-T002: CRUD de Racks. Falhas (400/404/409/401/403) são embrulhadas
/// pelo GlobalExceptionHandler — o controller só compõe sucesso no envelope.
/// </summary>
[Route("api/v1/inventory/racks")]
public sealed class RacksController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize(Roles = "Admin,User,Viewer")]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? locationId,
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchRacksQuery(locationId, term, page, pageSize), cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,User,Viewer")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetRackQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Create(
        [FromBody] CreateRackViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateRackCommand(model.LocationId, model.Name, model.Description),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateRackViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateRackCommand(id, model.Name, model.Description),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteRackCommand(id), cancellationToken);
        return NoContent();
    }
}

using InfraControl.Application.Inventory.Locations.Commands;
using InfraControl.Application.Inventory.Locations.Queries;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Inventory;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Inventory;

/// <summary>
/// F2-T001: CRUD de Locations. Falhas (400/404/409/401/403) são embrulhadas
/// pelo GlobalExceptionHandler — o controller só compõe sucesso no envelope.
/// </summary>
[Route("api/v1/inventory/locations")]
public sealed class LocationsController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize(Roles = "Admin,User,Viewer")]
    public async Task<IActionResult> Search(
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchLocationsQuery(term, page, pageSize), cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,User,Viewer")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetLocationQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Create(
        [FromBody] CreateLocationViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateLocationCommand(model.Name, model.Description),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateLocationViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateLocationCommand(id, model.Name, model.Description),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteLocationCommand(id), cancellationToken);
        return NoContent();
    }
}

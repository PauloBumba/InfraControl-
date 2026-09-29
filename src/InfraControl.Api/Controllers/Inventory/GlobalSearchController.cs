using InfraControl.Application.Inventory.GlobalSearch.Queries;
using InfraControl.Api.Controllers;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Inventory;

/// <summary>
/// F2-T007: busca global do inventário — um único GET agrega Location, Rack,
/// Device, NetworkInterface, MacAddress e IpAddress. Somente leitura; falhas
/// (400/401/403) embrulhadas pelo GlobalExceptionHandler.
/// </summary>
[Route("api/v1/inventory/search")]
public sealed class GlobalSearchController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? term,
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GlobalSearchQuery(term, limit), cancellationToken)));
}

using InfraControl.Application.Network.Switches.Queries;
using InfraControl.Api.Controllers;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Network;

/// <summary>
/// F3-T001: módulo Network — visão de switches reusando o fluxo de Device
/// (F2-T003), type=Switch fixo. Falhas (400/404/401/403) embrulhadas pelo
/// GlobalExceptionHandler; casa canônica das ports na F3-T002.
/// </summary>
[Route("api/v1/network/switches")]
public sealed class SwitchesController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "InventoryRead")]
    public async Task<IActionResult> Search(
        [FromQuery] string? term,
        [FromQuery] Guid? locationId,
        [FromQuery] Guid? rackId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchSwitchesQuery(term, page, pageSize, locationId, rackId),
            cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "InventoryRead")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetSwitchQuery(id), cancellationToken)));
}

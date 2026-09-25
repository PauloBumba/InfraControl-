using InfraControl.Application.Discovery.Runs.Queries;
using InfraControl.Api.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Discovery;

/// <summary>
/// F4-T001: execuções de discovery (somente leitura — o POST/gatilho manual
/// por device vem com os providers F4-T003/T004). Falhas embrulhadas pelo
/// GlobalExceptionHandler.
/// </summary>
[Route("api/v1/discovery")]
public sealed class DiscoveryRunsController(ISender sender) : BaseController
{
    [HttpGet("runs")]
    [Authorize(Policy = "MonitoringRead")]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? deviceId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchDiscoveryRunsQuery(deviceId, page, pageSize), cancellationToken)));

    [HttpGet("runs/{id:guid}")]
    [Authorize(Policy = "MonitoringRead")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetDiscoveryRunQuery(id), cancellationToken)));
}

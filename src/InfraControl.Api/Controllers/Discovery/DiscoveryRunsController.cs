using InfraControl.Application.Discovery.Runs;
using InfraControl.Application.Discovery.Runs.Queries;
using InfraControl.Api.ViewModels.Discovery;
using InfraControl.Api.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Discovery;

/// <summary>
/// F4-T001/T003: execuções de discovery — leitura (T001) + gatilho
/// manual por device (T003, provider ICMP). Falhas embrulhadas pelo
/// GlobalExceptionHandler.
/// </summary>
[Route("api/v1/discovery")]
public sealed class DiscoveryRunsController(ISender sender) : BaseController
{
    [HttpGet("runs")]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? deviceId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchDiscoveryRunsQuery(deviceId, page, pageSize), cancellationToken)));

    [HttpGet("runs/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetDiscoveryRunQuery(id), cancellationToken)));

    /// <summary>Gatilho manual de discovery (F4-T003) — MonitoringManage.</summary>
    [HttpPost("devices/{deviceId:guid}/runs")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Start(
        Guid deviceId,
        [FromBody] StartDiscoveryViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new StartDiscoveryCommand(deviceId, model.Provider), cancellationToken)));
}

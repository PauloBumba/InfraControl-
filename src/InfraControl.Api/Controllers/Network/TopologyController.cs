using InfraControl.Api.Controllers;
using InfraControl.Application.Network.Topology;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Network;

/// <summary>
/// F3-T006: grafo topológico (nodes = interfaces, edges = Connections,
/// status = último Ping do device). Só leitura; sem DiscoverTopology —
/// discovery/ingestão já vivem em F4 + F3-T005b.
/// </summary>
[Route("api/v1/network/topology")]
public sealed class TopologyController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTopologyQuery(), cancellationToken)));
}

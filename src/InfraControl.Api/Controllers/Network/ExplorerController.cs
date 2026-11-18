using InfraControl.Api.Controllers;
using InfraControl.Application.Network.Explorer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Network;

/// <summary>
/// Network Explorer F2 — detalhe do switch (portas + VLANs + MACs aprendidos)
/// e VLAN Explorer (VLANs cadastradas + descobertas-não-cadastradas).
/// Somente consulta.
/// </summary>
[Route("api/v1/network/explorer")]
public sealed class ExplorerController(ISender sender) : BaseController
{
    [HttpGet("switches/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Switch(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetSwitchExplorerQuery(id), cancellationToken)));

    /// <summary>APs e controladores (ACs) do inventário + UniFi ao vivo.</summary>
    [HttpGet("aps")]
    [Authorize]
    public async Task<IActionResult> Aps(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchApExplorerQuery(), cancellationToken)));

    [HttpGet("vlans")]
    [Authorize]
    public async Task<IActionResult> Vlans(
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchVlansExplorerQuery(term, page, pageSize), cancellationToken)));
}

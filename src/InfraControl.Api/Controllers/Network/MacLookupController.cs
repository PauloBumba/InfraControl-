using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Network;
using InfraControl.Application.Network.Explorer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Network;

/// <summary>
/// Network Explorer F1 — MAC/IP Explorer: pesquisa única que cruza as
/// evidências de discovery (tabela MAC/SNMP, ARP, DHCP, LLDP) e enriquece
/// com AP Wi-Fi quando o UniFi souber. Somente consulta.
/// </summary>
[Route("api/v1/network")]
public sealed class MacLookupController(ISender sender) : BaseController
{
    [HttpGet("mac-lookup")]
    [Authorize]
    public async Task<IActionResult> Lookup(
        [FromQuery] string term,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchMacLookupQuery(term), cancellationToken)));
}

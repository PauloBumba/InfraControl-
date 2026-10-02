using InfraControl.Application.Discovery.Clients.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers;

/// <summary>
/// F18-T008: clientes da rede (IP/MAC/hostname/vlan) agregados de TODAS as
/// execuções de discovery (ARP/DHCP) — a lista não depende do último run
/// (decisão do F17-T015). Somente leitura; falhas embrulhadas pelo
/// GlobalExceptionHandler.
/// </summary>
[Route("api/v1/clients")]
public sealed class ClientsController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? term = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchClientsQuery(term, page, pageSize), cancellationToken)));
}

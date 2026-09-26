using InfraControl.Application.Discovery.Results.Queries;
using InfraControl.Api.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Discovery;

/// <summary>
/// F4-T002: resultados (achados) de discovery — somente leitura; a escrita
/// vem com os providers F4-T003+. Falhas embrulhadas pelo
/// GlobalExceptionHandler (run desconhecido → 404).
/// </summary>
[Route("api/v1/discovery")]
public sealed class DiscoveryResultsController(ISender sender) : BaseController
{
    [HttpGet("runs/{runId:guid}/results")]
    [Authorize(Roles = "Admin,User,Viewer")]
    public async Task<IActionResult> Search(
        Guid runId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchDiscoveryResultsQuery(runId, page, pageSize), cancellationToken)));

    [HttpGet("results/{id:guid}")]
    [Authorize(Roles = "Admin,User,Viewer")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetDiscoveryResultQuery(id), cancellationToken)));
}

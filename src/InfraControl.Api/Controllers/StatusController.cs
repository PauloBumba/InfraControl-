using InfraControl.Application.Operations.Cluster;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers;

/// <summary>
/// Tela de status (F50): visão de leitura do parque de containers para
/// qualquer role autenticada — escala/remoção fica no ClusterController
/// (Administer).
/// </summary>
[Route("api/v1/status")]
public sealed class StatusController(ISender sender) : BaseController
{
    /// <summary>Containers do projeto (estado atual; sem stats — caminho barato).</summary>
    [HttpGet("containers")]
    [Authorize]
    public async Task<IActionResult> Containers(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetDockerStatusQuery(), cancellationToken)));
}

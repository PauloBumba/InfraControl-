using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.Reports.Transversal.Queries;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Reports;

/// <summary>
/// Relatório transversal (ADR-049) — visão agregada de cima dos 5 módulos
/// (produtividade/PDI, tasks, knowledge, projetos, incidentes). Read-model
/// composto, sem armazenamento novo. <c>Operate</c> (ADR-030): snapshot
/// global de operações para o Centro de Operações; dashboards por vertical
/// continuam sendo a visão pessoal/gestor.
/// </summary>
[Route("api/v1/reports")]
public sealed class ReportsController(ISender sender) : BaseController
{
    /// <summary>Snapshot agregado; filtros de período opcionais (de/até).</summary>
    [HttpGet("transversal")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetTransversal(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTransversalReportQuery(from, to), cancellationToken)));
}

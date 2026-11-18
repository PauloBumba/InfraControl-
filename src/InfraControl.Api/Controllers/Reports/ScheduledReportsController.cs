using InfraControl.Application.Reports.Scheduled;
using InfraControl.Api.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Reports;

/// <summary>
/// Agenda do relatório geral por e-mail: leitura para quem tem Read,
/// upsert (cria/atualiza a agenda "General") somente Admin.
/// </summary>
[Route("api/v1/reports")]
public sealed class ScheduledReportsController(ISender sender) : BaseController
{
    [HttpGet("schedule")]
    [Authorize]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetScheduledReportQuery(), cancellationToken)));

    [HttpPut("schedule")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> Upsert(
        [FromBody] ScheduledReportUpsertInput input,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpsertScheduledReportCommand(input), cancellationToken)));
}

using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.Dashboard.Queries;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Dashboard;

/// <summary>
/// F54-T002 (docs/69): endpoints de leitura do dashboard principal —
/// visão geral (KPIs globais) no padrão da casa: envelope ApiResponse e
/// falhas embrulhadas pelo GlobalExceptionHandler. O query exige
/// RoleAccess.Read via IAuthorizedRequest; aqui é só [Authorize].
/// </summary>
[Route("api/v1/dashboard")]
public sealed class DashboardController(ISender sender) : BaseController
{
    /// <summary>
    /// Visão geral: contagens de dispositivos (tipo + status local),
    /// alertas abertos, incidentes ativos e suprimentos de impressora.
    /// Cache de 60s no CachingBehavior (F54-T002).
    /// </summary>
    [HttpGet("overview")]
    [Authorize]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetDashboardOverviewQuery(), cancellationToken)));

    /// <summary>
    /// Séries dos gráficos (polls, alertas, incidentes) — período
    /// 1h|24h|7d|30d, bucket de5min/1h/6h/1d (F54-T003). Período inválido
    /// → 400 pt-BR via GlobalExceptionHandler.
    /// </summary>
    [HttpGet("series")]
    [Authorize]
    public async Task<IActionResult> Series(
        [FromQuery] string period, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetDashboardSeriesQuery(period), cancellationToken)));
}

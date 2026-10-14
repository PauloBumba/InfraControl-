using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.Pdi.Queries;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Operations;

/// <summary>
/// Dashboards do PDI (docs/30 §18–§20, ADR-037) — os 3 queries já
/// existiam no Application, mas nenhum controller expunha as rotas que o
/// front invocava (PdiPage/PdiReportsDialog) — expostas aqui.
/// Colaborador (§18) e gestor (§19) são Operate com fine-grained no
/// handler (PdiAccess.EnsureCanView/EnsureCanReport); o administrativo
/// (§20) é Administer — policy e RequiredRoles iguais (ADR-030 §3).
/// </summary>
[Route("api/v1/pdi")]
public sealed class PdiDashboardController(ISender sender) : BaseController
{
    /// <summary>§18 — "Meu PDI": progresso, fases, prazo e pendências.</summary>
    [HttpGet("{userId:guid}/dashboard")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetDashboard(
        Guid userId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetPdiDashboardQuery(userId), cancellationToken)));

    /// <summary>§19 — "Meus PDIs": planos em que sou gestor (🟢/🟡/🔴).</summary>
    [HttpGet("manager/dashboard")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetManagerDashboard(
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetPdiManagerDashboardQuery(), cancellationToken)));

    /// <summary>§20 — visão administrativa (contagens e taxa de conclusão).</summary>
    [HttpGet("admin/dashboard")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> GetAdminDashboard(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetPdiAdminDashboardQuery(from, to), cancellationToken)));
}

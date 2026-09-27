using InfraControl.Application.Alerts.Queries;
using InfraControl.Application.Monitoring.Checks;
using InfraControl.Application.Monitoring.Results;
using InfraControl.Api.Controllers;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Monitoring;

/// <summary>
/// Monitoramento: execução via RunCheckCommand (docs/21 MON-006/007 —
/// ADR-006: queries apenas leem; leitura de perfil/histórico via GET
/// checks/results). Falhas (400/404/401/403) são embrulhadas pelo
/// GlobalExceptionHandler — o controller só compõe sucesso.
/// </summary>
[Route("api/v1/monitoring")]
public sealed class MonitoringController(ISender sender) : BaseController
{
    /// <summary>Executa um check num device agora e grava o resultado (MON-006).</summary>
    [HttpPost("run-check/{id:guid}")]
    [Authorize(Roles = "Admin,User,Viewer")]
    public async Task<IActionResult> RunCheck(
        Guid id,
        [FromQuery] string check,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(
            await sender.Send(new RunCheckCommand(id, check), cancellationToken)));

    /// <summary>Executa um check em todos os devices (estilo Zabbix "check now").</summary>
    [HttpPost("run-check")]
    [Authorize(Roles = "Admin,User,Viewer")]
    public async Task<IActionResult> RunAllChecks(
        [FromQuery] string check,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(
            await sender.Send(new RunAllChecksCommand(check), cancellationToken)));

    /// <summary>Checks monitoráveis de um device, vindos do perfil (F5-T001).</summary>
    [HttpGet("checks/{id:guid}")]
    [Authorize(Roles = "Admin,User,Viewer")]
    public async Task<IActionResult> GetChecks(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetDeviceChecksQuery(id), cancellationToken)));

    /// <summary>Histórico de polls do device, mais recente primeiro (F5-T002).</summary>
    [HttpGet("results/{id:guid}")]
    [Authorize(Roles = "Admin,User,Viewer")]
    public async Task<IActionResult> GetResults(
        Guid id,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetDeviceResultsQuery(id, limit), cancellationToken)));

    /// <summary>
    /// Alertas paginados (F6-T001) — criação é automática pós-check;
    /// status: open|resolved|vazio = todos.
    /// </summary>
    [HttpGet("alerts")]
    [Authorize(Roles = "Admin,User,Viewer")]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] Guid? deviceId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchAlertsQuery(deviceId, status, page, pageSize),
            cancellationToken)));
}

using InfraControl.Application.Alerts.Queries;
using InfraControl.Application.Monitoring.Checks;
using InfraControl.Application.Monitoring.Prevention;
using InfraControl.Application.Monitoring.Results;
using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Monitoring;
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
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> RunCheck(
        Guid id,
        [FromQuery] string check,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(
            await sender.Send(new RunCheckCommand(id, check), cancellationToken)));

    /// <summary>Executa um check em todos os devices (estilo Zabbix "check now").</summary>
    [HttpPost("run-check")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> RunAllChecks(
        [FromQuery] string check,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(
            await sender.Send(new RunAllChecksCommand(check), cancellationToken)));

    /// <summary>
    /// F30-T004 S4 (ADR-056): avaliação de prevenção WiFi de um AP — lê o
    /// histórico do Zabbix (janela de 60 min) e abre/resolve o Alert pelo
    /// pipeline existente. Opt-in = device com SnmpRadio no perfil; sem
    /// pré-requisito a resposta é honesta (Evaluated=false + motivo) —
    /// nunca um 2xx "saudável" inventado.
    /// </summary>
    [HttpPost("wifi-prevention/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> EvaluateWifiPrevention(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new EvaluateWifiPreventionCommand(id), cancellationToken)));

    /// <summary>Checks monitoráveis de um device, vindos do perfil (F5-T001).</summary>
    [HttpGet("devices/{deviceId:guid}/checks")]
    [Authorize]
    public async Task<IActionResult> GetChecks(Guid deviceId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetDeviceChecksQuery(deviceId), cancellationToken)));

    // ── CRUD de MonitoringCheck (ADR-030 §1) ──────────────────────────────

    /// <summary>Uma check por id (ADR-030 §2 — get by id).</summary>
    [HttpGet("checks/{checkId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetCheck(Guid checkId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetMonitoringCheckQuery(checkId), cancellationToken)));

    /// <summary>Adiciona uma check ao device (duplicidade = 409).</summary>
    [HttpPost("checks")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> CreateCheck(
        [FromBody] CreateMonitoringCheckViewModel model,
        CancellationToken cancellationToken)
        => StatusCode(
            StatusCodes.Status201Created,
            ApiResponse.Ok(await sender.Send(
                new CreateMonitoringCheckCommand(
                    model.DeviceId, model.Check, model.Enabled),
                cancellationToken)));

    /// <summary>Toggle de habilitação da check.</summary>
    [HttpPut("checks/{checkId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UpdateCheck(
        Guid checkId,
        [FromBody] UpdateMonitoringCheckViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpdateMonitoringCheckCommand(checkId, model.Enabled),
            cancellationToken)));

    /// <summary>Remove a check do device (o sync do profile pode recriá-la).</summary>
    [HttpDelete("checks/{checkId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DeleteCheck(
        Guid checkId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteMonitoringCheckCommand(checkId), cancellationToken);
        return NoContent();
    }

    /// <summary>Histórico de polls do device, mais recente primeiro (F5-T002).</summary>
    [HttpGet("results/{id:guid}")]
    [Authorize]
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
    [Authorize]
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

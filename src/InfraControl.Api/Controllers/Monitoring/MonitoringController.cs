using InfraControl.Application.Monitoring.Checks;
using InfraControl.Application.Monitoring.Ping;
using InfraControl.Application.Monitoring.PrinterSupplies;
using InfraControl.Api.Controllers;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Monitoring;

/// <summary>
/// F5-T003: ping sob demanda (up/down estilo Zabbix). Falhas (400/404/401/403)
/// são embrulhadas pelo GlobalExceptionHandler — o controller só compõe sucesso.
/// </summary>
[Route("api/v1/monitoring")]
public sealed class MonitoringController(ISender sender) : BaseController
{
    /// <summary>Status de ping de um device específico.</summary>
    [HttpGet("ping/{id:guid}")]
    [Authorize(Policy = "MonitoringRead")]
    public async Task<IActionResult> PingDevice(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new PingDeviceQuery(id), cancellationToken)));

    /// <summary>Up/down de todos os devices (com IP), ordenado por nome.</summary>
    [HttpGet("ping")]
    [Authorize(Policy = "MonitoringRead")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetPingStatusQuery(), cancellationToken)));

    /// <summary>Toner e drum de uma impressora via SNMP (F5-T004).</summary>
    [HttpGet("printers/{id:guid}/supplies")]
    [Authorize(Policy = "MonitoringRead")]
    public async Task<IActionResult> GetPrinterSupplies(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(
            await sender.Send(new GetPrinterSuppliesQuery(id), cancellationToken)));

    /// <summary>Checks monitoráveis de um device, vindos do perfil (F5-T001).</summary>
    [HttpGet("checks/{id:guid}")]
    [Authorize(Policy = "MonitoringRead")]
    public async Task<IActionResult> GetChecks(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetDeviceChecksQuery(id), cancellationToken)));
}

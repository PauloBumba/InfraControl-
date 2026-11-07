using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.Notifications.Routing;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Notifications;

/// <summary>
/// F39-T003 (ADR-063): política de roteamento de notificações — defaults
/// globais + regras por EventType com filtros Zabbix (severidade 0–5, host,
/// grupo, trigger, tags). Política Administer; versão monotônica registrada
/// a cada gravação.
/// </summary>
[Route("api/v1/notifications/routing")]
[Authorize(Policy = AuthorizationPolicies.Administer)]
public sealed class NotificationRoutingController(ISender sender) : BaseController
{
    /// <summary>Estado efetivo da política (sem linha = legado, Version=0).</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetNotificationRoutingQuery(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>Salva defaults + regras (estado completo; regras ausentes do payload são removidas).</summary>
    [HttpPut]
    public async Task<IActionResult> Save(
        [FromBody] SaveNotificationRoutingCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}

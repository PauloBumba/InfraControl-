using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.Notifications.Rules;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Notifications;

/// <summary>
/// F42-T001: regras de notificação por tipo de evento (granularidade
/// "incidente → email", "tarefa → desligado" etc.). Sem regra = fanout
/// global atual (zero regressão). Política Administer.
/// </summary>
[Route("api/v1/notifications/rules")]
[Authorize(Policy = AuthorizationPolicies.Administer)]
public sealed class NotificationEventRulesController(ISender sender) : BaseController
{
    /// <summary>Catálogo completo (13+ eventos) + regras persistidas.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetNotificationEventRulesQuery(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>Salva regras em lote (upsert por EventType).</summary>
    [HttpPut]
    public async Task<IActionResult> Save(
        [FromBody] SaveNotificationEventRulesCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}

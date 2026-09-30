using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Integrations;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InfraControl.Api.Controllers.Integrations;

/// <summary>
/// F16-T001/ADR-023: webhook de entrada do Zabbix — primeiro endpoint
/// público da API ([AllowAnonymous] explícito + entrada na allowlist do arch
/// test Public_Endpoints_Should_Be_The_Explicit_Allowlist). A autenticação é
/// o header X-Zabbix-Token vs Integrations:Zabbix:Token: token vazio = 404
/// (integração desligada), ausente/divergente = 401 — sempre no envelope
/// ApiResponse. Falhas de domínio (400 pt-BR) saem pelo GlobalExceptionHandler.
/// </summary>
[Route("api/v1/integrations/zabbix")]
public sealed class ZabbixIntegrationController(
    ISender sender, IOptions<ZabbixIntegrationOptions> options) : BaseController
{
    /// <summary>Recebe PROBLEM/OK do action do Zabbix (contrato em docs/27).</summary>
    [HttpPost("problems")]
    [AllowAnonymous]
    public async Task<IActionResult> Problems(
        [FromBody] IngestZabbixProblemViewModel model,
        CancellationToken cancellationToken)
    {
        var configuredToken = options.Value.Token;
        if (string.IsNullOrWhiteSpace(configuredToken))
            return NotFound(ApiResponse.Fail(
                ["A integração com Zabbix está desativada."]));

        if (!Request.Headers.TryGetValue(
                ZabbixIntegrationOptions.TokenHeaderName, out var providedToken)
            || !string.Equals(
                providedToken.ToString(), configuredToken, StringComparison.Ordinal))
            return Unauthorized(ApiResponse.Fail(["Token do Zabbix inválido."]));

        var result = await sender.Send(
            new IngestZabbixProblemCommand(
                model.EventId, model.Host, model.Ip,
                model.Name, model.Severity, model.Status),
            cancellationToken);

        return Ok(ApiResponse.Ok(result));
    }
}

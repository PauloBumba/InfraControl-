using InfraControl.Api.Authorization;
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
/// F27 Fase 2.2: configuração administrativa da integração de SAÍDA para a
/// API do Zabbix (Administração → Integrações → Zabbix) — política
/// Administer, senha write-only e cifrada, nunca devolvida.
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
                model.EventId, model.RecoveryEventId, model.Host, model.Ip,
                model.Name, model.Severity, model.Status, model.OccurredAt),
            cancellationToken);

        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// F27 Fase 2.2: configuração atual (nunca expõe a senha — só se está
    /// definida, de qual fonte vem e o estado/versão do último teste).
    /// </summary>
    [HttpGet("config")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> GetConfig(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetZabbixSettingsQuery(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// F27 Fase 2.2: cria/atualiza a configuração persistida. Senha em
    /// texto puro no request, cifrada imediatamente (AES-GCM) e nunca
    /// devolvida; omitir a senha mantém a já gravada.
    /// </summary>
    [HttpPut("config")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> SaveConfig(
        [FromBody] SaveZabbixSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SaveZabbixSettingsCommand(request), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// F27 Fase 2.2: teste de conexão com a configuração efetiva (banco →
    /// env) — consulta apiinfo.version e persiste versão/estado/erro na
    /// linha de configuração. Nunca lança por falha de conexão; o erro
    /// identificável vem no resultado, sem segredos.
    /// </summary>
    [HttpPost("test-connection")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> TestConnection(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TestZabbixConnectionCommand(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// F27 Fase 3.1: sincroniza os hosts do Zabbix → read-model local
    /// (idempotente por hostid; removidos no Zabbix viram ausentes, nunca
    /// são apagados). Falha de conexão/credencial propaga (502/503).
    /// </summary>
    [HttpPost("sync")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SyncZabbixInventoryCommand(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// F27 Fase 3.3 (backend): hosts sincronizados com busca (nome/IP/
    /// hostid), filtros de estado/ausência e flag de match com o
    /// inventário (HasAsset).
    /// </summary>
    [HttpGet("hosts")]
    [Authorize]
    public async Task<IActionResult> GetHosts(
        [FromQuery] GetZabbixHostsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(query, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}

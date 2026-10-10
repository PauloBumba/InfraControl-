using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Insights;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Integrations;

/// <summary>
/// F20-T001/ADR-028 §5: integração de SAÍDA com o UniFi Controller. O sync
/// é escrita de nível Operate — a policy HTTP (Operate) declara a 1ª camada
/// e o IAuthorizedRequest do command a 2ª, mesmo nível nas duas (ADR-030 §3);
/// nenhum literal de role (regra do arch test) e a proteção CSRF é do
/// middleware global (AntiforgeryCookieMiddleware). O
/// health é sondagem ao vivo em tempo real; o agregado persistido da
/// integração fica para task própria (decisão do usuário 03/10/2026).
/// </summary>
[Authorize]
[Route("api/v1/integrations/unifi")]
public sealed class UnifiIntegrationController(
    ISender sender, IUnifiApiClient unifiApiClient) : BaseController
{
    /// <summary>
    /// Sincroniza o cadastro do Controller → inventory (idempotente por MAC).
    /// Resumo em <see cref="UnifiSyncResultDto"/>.
    /// </summary>
    [HttpPost("sync")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SyncUnifiInventoryCommand(), cancellationToken);

        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Sondagem leve (GET /api/self no Controller) — não persiste nada.
    /// Indisponível/desativado = 503 com o motivo no envelope.
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        var healthy = await unifiApiClient.IsHealthyAsync(cancellationToken);
        if (!healthy)
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse.Fail(
                    ["UniFi Controller indisponível (ou integração não configurada)."]));

        return Ok(ApiResponse.Ok<object>(new { status = "up" }));
    }

    /// <summary>
    /// F18-T007: painéis reais da InsightsPage — Tráfego por Cliente e
    /// DPI por Aplicação, sondados ao vivo no Controller (nada é
    /// persistido, regra 8). Integração não configurada/Controller fora
    /// ou sessão recusada = 503 com o motivo no envelope (mesma
    /// semântica do <see cref="Health"/>); leitura de dashboard = só
    /// autenticado (roles no IAuthorizedRequest, nível único ADR-030 §3).
    /// </summary>
    [HttpGet("insights")]
    public async Task<IActionResult> Insights(CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new GetUnifiInsightsQuery(), cancellationToken);
            return Ok(ApiResponse.Ok(result));
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or UnauthorizedAccessException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse.Fail([exception.Message]));
        }
    }

    /// <summary>
    /// F26 Fase 2.2: configuração atual (nunca expõe a senha — só se
    /// está definida e de qual fonte vem: banco ou env).
    /// </summary>
    [HttpGet("config")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> GetConfig(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUniFiSettingsQuery(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// F26 Fase 2.2: cria/atualiza a configuração persistida. Senha em
    /// texto puro no request, cifrada imediatamente (AES-GCM) e nunca
    /// devolvida; omitir a senha mantém a já gravada.
    /// </summary>
    [HttpPut("config")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> SaveConfig(
        [FromBody] SaveUniFiSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SaveUniFiSettingsCommand(request), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// F26 Fase 3.1: sites persistidos (leitura — sincronizados no sync de
    /// inventário; site ausente no controller aparece como `Missing`).
    /// </summary>
    [HttpGet("sites")]
    public async Task<IActionResult> GetSites(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUniFiSitesQuery(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// F26 Fase 4.1: clientes conectados AO VIVO (stat/sta) — nunca
    /// persistidos (REGRA 8); "desconectado" é ausência nesta consulta,
    /// não um estado gravado.
    /// </summary>
    [HttpGet("clients")]
    public async Task<IActionResult> GetClients(CancellationToken cancellationToken)
    {
        var client = await sender.Send(new GetUnifiLiveClientsQuery(), cancellationToken);
        return Ok(ApiResponse.Ok(client));
    }

    /// <summary>
    /// F26 Fase 5: portas do switch com o MAC informado (port_table ao
    /// vivo — REGRA 8: nada é persistido; "down" só quando o controller
    /// respondeu).
    /// </summary>
    [HttpGet("switches/{mac}/ports")]
    public async Task<IActionResult> GetSwitchPorts(
        string mac, CancellationToken cancellationToken)
    {
        var ports = await sender.Send(
            new GetUnifiLivePortsQuery(mac), cancellationToken);
        return Ok(ApiResponse.Ok(ports));
    }

    /// <summary>F26 Fase 9.3: histórico de sincronizações (filtros).</summary>
    [HttpGet("sync-runs")]
    public async Task<IActionResult> GetSyncRuns(
        [FromQuery] int limit = 50,
        [FromQuery] bool? succeeded = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var runs = await sender.Send(
            new ListUniFiSyncRunsQuery(limit, succeeded, fromUtc, toUtc), cancellationToken);
        return Ok(ApiResponse.Ok(runs));
    }

    /// <summary>
    /// F26 Fase 8: dashboard UniFi — persistido × ao vivo separados no DTO.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var dashboard = await sender.Send(new GetUnifiDashboardQuery(), cancellationToken);
        return Ok(ApiResponse.Ok(dashboard));
    }

    /// <summary>
    /// F26 Fase 7: correlação UniFi↔inventário (matched/unlinked/conflitos)
    /// sobre ExternalDeviceReference.
    /// </summary>
    [HttpGet("correlation")]
    public async Task<IActionResult> GetCorrelation(CancellationToken cancellationToken)
    {
        var correlation = await sender.Send(new GetUniFiCorrelationQuery(), cancellationToken);
        return Ok(ApiResponse.Ok(correlation));
    }

    /// <summary>F26 Fase 7.2: vínculo manual MAC→Device (só Administer).</summary>
    [HttpPost("links")]
    public async Task<IActionResult> CreateLink(
        [FromBody] CreateUniFiAssetLinkCommand command, CancellationToken cancellationToken)
    {
        var link = await sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(link));
    }

    /// <summary>
    /// F26 Fase 7.3: remove SÓ o vínculo — o histórico do Device é
    /// preservado (só Administer).
    /// </summary>
    [HttpDelete("links/{mac}")]
    public async Task<IActionResult> DeleteLink(string mac, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteUniFiAssetLinkCommand(mac), cancellationToken);
        return Ok(ApiResponse.Ok<object>(new { }));
    }

    /// <summary>
    /// F26 Fase 6: topologia derivada do stat/device (uplink + LLDP quando
    /// presente) — ao vivo, nunca persistida (REGRA 8).
    /// </summary>
    [HttpGet("topology")]
    public async Task<IActionResult> GetTopology(CancellationToken cancellationToken)
    {
        var topology = await sender.Send(new GetUnifiTopologyQuery(), cancellationToken);
        return Ok(ApiResponse.Ok(topology));
    }

    /// <summary>F26 Fase 4.2: redes/SSIDs configurados (rest/networkconf).</summary>
    [HttpGet("networks")]
    public async Task<IActionResult> GetNetworks(CancellationToken cancellationToken)
    {
        var networks = await sender.Send(new GetUnifiNetworksQuery(), cancellationToken);
        return Ok(ApiResponse.Ok(networks));
    }

    /// <summary>
    /// F26 Fase 2.2: teste de conexão com a configuração efetiva (banco →
    /// env). Nunca lança por falha de conexão — o erro identificável vem no
    /// resultado.
    /// </summary>
    [HttpPost("test-connection")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> TestConnection(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TestUniFiConnectionCommand(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}

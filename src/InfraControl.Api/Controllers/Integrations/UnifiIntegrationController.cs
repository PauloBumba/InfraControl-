using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.Integrations.Unifi;
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
}

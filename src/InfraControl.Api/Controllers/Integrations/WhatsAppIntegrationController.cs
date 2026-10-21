using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.Integrations.WhatsApp;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Integrations;

/// <summary>
/// ADR-062: gestão do canal WhatsApp (Evolution API) dentro da plataforma —
/// configuração persistida em banco (padrão UniFi/Zabbix), conexão (QR),
/// grupos e teste de envio. Política Administer em todos os endpoints de
/// escrita; a ApiKey é write-only (cifrada AES-GCM) e nunca devolvida.
/// O envio de EVENTOS continua pelo pipeline Outbox → RabbitMQ → worker →
/// dispatcher (ADR-059 decisão 5) — este controller só GERENCIA o canal.
/// </summary>
[Route("api/v1/integrations/whatsapp")]
[Authorize(Policy = AuthorizationPolicies.Administer)]
public sealed class WhatsAppIntegrationController(ISender sender) : BaseController
{
    /// <summary>Configuração atual (nunca expõe a ApiKey — só se está definida e a fonte).</summary>
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetWhatsAppSettingsQuery(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Cria/atualiza a configuração persistida. ApiKey em texto puro no
    /// request, cifrada imediatamente (AES-GCM) e nunca devolvida; omitir a
    /// ApiKey mantém a já gravada. GroupId (grupo @g.us) é o destino.
    /// </summary>
    [HttpPut("config")]
    public async Task<IActionResult> SaveConfig(
        [FromBody] SaveWhatsAppSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SaveWhatsAppSettingsCommand(request), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>Estado de conexão da instância (open/connecting/close).</summary>
    [HttpGet("connection-state")]
    public async Task<IActionResult> GetConnectionState(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetWhatsAppConnectionStateQuery(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Gera um novo QR de pareamento (faz logout best-effort + connect —
    /// workaround #1408 do spike). Devolve o PNG do QR para a UI exibir.
    /// </summary>
    [HttpPost("connect")]
    public async Task<IActionResult> Connect(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GenerateWhatsAppQrCommand(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>Desconecta a instância (logout) e registra o estado.</summary>
    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DisconnectWhatsAppInstanceCommand(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>Cria a instância na Evolution se não existir (best-effort).</summary>
    [HttpPost("instance")]
    public async Task<IActionResult> CreateInstance(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateWhatsAppInstanceCommand(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>Lista os grupos do número conectado (para escolher o destino).</summary>
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListWhatsAppGroupsQuery(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Envia uma mensagem de teste no destino configurado (GroupId ?? Phone)
    /// e persiste o resultado na linha de configuração.
    /// </summary>
    [HttpPost("test")]
    public async Task<IActionResult> Test(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SendWhatsAppTestCommand(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}

using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.Integrations.Email;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Integrations;

/// <summary>
/// F41-T003: configuração administrativa do canal de e-mail SMTP
/// (Administração → Integrações → Email). Política Administer, senha
/// write-only e cifrada, nunca devolvida. Cobre os dois gatilhos:
/// notificações (canal Email) e identity (convite/forgot-password).
/// </summary>
[Route("api/v1/integrations/email")]
[Authorize(Policy = AuthorizationPolicies.Administer)]
public sealed class EmailIntegrationController(ISender sender) : BaseController
{
    /// <summary>Configuração atual (nunca expõe a senha — só se está definida e o estado do último teste).</summary>
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetEmailSettingsQuery(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>Cria/atualiza a configuração persistida. Senha em texto puro no request, cifrada imediatamente (AES-GCM) e nunca devolvida; omitir a senha mantém a já gravada.</summary>
    [HttpPut("config")]
    public async Task<IActionResult> SaveConfig(
        [FromBody] SaveEmailSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SaveEmailSettingsCommand(request), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>Teste de envio com a configuração efetiva (banco → env) — envia e-mail de prova ao remetente e persiste estado/erro na linha. Nunca lança por falha de conexão.</summary>
    [HttpPost("test")]
    public async Task<IActionResult> Test(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SendEmailTestCommand(), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}

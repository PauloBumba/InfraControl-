using InfraControl.Api.Controllers;
using InfraControl.Api.Middleware;
using InfraControl.Application.Identity.Sessions;
using InfraControl.BuildingBlocks.Application;
using InfraControl.BuildingBlocks.Application.Response;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Identity;

/// <summary>
/// F1-T016b / ADR-020: auto-serviço de sessões — listar as próprias,
/// revogar uma e "sair de todos os outros". Qualquer role autenticada mexe
/// nas PRÓPRIAS sessões (mesma lógica do MFA: [Authorize] sem exigir
/// Operate/Administer — um Viewer também precisa enxergar onde está logado).
/// Escrita por cookie já passa pela validação de antiforgery do pipeline
/// (ADR-017). A revogação grava no banco E no cache compartilhado: é a
/// segunda escrita que derruba o cookie da outra instância na hora.
/// </summary>
[Route("api/v1/auth/sessions")]
[Authorize]
public sealed class SessionsController(
    ISessionService sessionService,
    ICurrentUserContext currentUser) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null) return Unauthorized(ApiResponse.Fail(["Usuário não autenticado."]));

        var sessions = await sessionService.ListAsync(userId.Value, CurrentSessionId, cancellationToken);
        return Ok(ApiResponse.Ok(sessions));
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> Get(Guid sessionId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null) return Unauthorized(ApiResponse.Fail(["Usuário não autenticado."]));

        // Alheia/expirada/revogada = MESMO 404 do revoke — sem oráculo de sid.
        var item = await sessionService.GetAsync(
            userId.Value, sessionId, CurrentSessionId, cancellationToken);
        return item is null
            ? NotFound(ApiResponse.Fail(["Sessão não encontrada."]))
            : Ok(ApiResponse.Ok(item));
    }

    [HttpDelete("{sessionId:guid}")]
    public async Task<IActionResult> Revoke(Guid sessionId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null) return Unauthorized(ApiResponse.Fail(["Usuário não autenticado."]));

        // Inexistente e alheia respondem a MESMA mensagem vinda do serviço —
        // 404 (não 403) para não confirmar que o sid existe.
        var result = await sessionService.RevokeAsync(userId.Value, sessionId, cancellationToken);
        if (!result.Succeeded)
            return NotFound(ApiResponse.Fail(result.Errors));

        // É a sessão atual: o cookie morre junto, sem esperar o middleware
        // recusar a próxima navegação com 401.
        if (CurrentSessionId == sessionId)
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        return Ok(ApiResponse<object>.Ok());
    }

    [HttpDelete("others")]
    public async Task<IActionResult> RevokeOthers(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null) return Unauthorized(ApiResponse.Fail(["Usuário não autenticado."]));

        var result = await sessionService.RevokeOthersAsync(userId.Value, CurrentSessionId, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<object>.Ok())
            : BadRequest(ApiResponse.Fail(result.Errors));
    }

    /// <summary>
    /// O sid do cookie. O ActiveSessionMiddleware é fail-closed (cookie sem
    /// sid nunca chega aqui); o parse defensivo existe para não assumir isso
    /// em camada de transporte.
    /// </summary>
    private Guid? CurrentSessionId =>
        Guid.TryParse(User.FindFirst(ActiveSessionMiddleware.SessionIdClaim)?.Value, out var sessionId)
            ? sessionId
            : null;
}

using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Identity;
using InfraControl.Application.Identity.Authentication;
using InfraControl.BuildingBlocks.Application;
using InfraControl.BuildingBlocks.Application.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Identity;

/// <summary>
/// MFA TOTP — auto-serviço da conta autenticada (F1-T016a, ADR-019).
/// Qualquer role autenticada mexe na PRÓPRIA conta, então os endpoints pedem
/// apenas [Authorize] (sem exigir Operate/Administer: um Viewer também precisa
/// poder proteger o próprio acesso). Escrita por cookie já passa pela validação
/// de antiforgery do pipeline (ADR-017) — aqui não há código extra para isso.
/// </summary>
[Route("api/v1/auth/mfa")]
[Authorize]
public sealed class MfaController(
    IIdentityAuthenticationService authenticationService,
    ICurrentUserContext currentUser) : BaseController
{
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null) return Unauthorized(ApiResponse.Fail(["Usuário não autenticado."]));

        var status = await authenticationService.GetMfaStatusAsync(userId.Value, cancellationToken);
        return Ok(ApiResponse.Ok(status));
    }

    [HttpPost("setup")]
    public async Task<IActionResult> Setup(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null) return Unauthorized(ApiResponse.Fail(["Usuário não autenticado."]));

        var setup = await authenticationService.SetupMfaAsync(userId.Value, cancellationToken);
        return Ok(ApiResponse.Ok(setup));
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(MfaConfirmViewModel model, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null) return Unauthorized(ApiResponse.Fail(["Usuário não autenticado."]));

        var result = await authenticationService.ConfirmMfaAsync(userId.Value, model.Code, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse.Ok(result))
            : BadRequest(ApiResponse.Fail(result.Errors));
    }

    [HttpPost("disable")]
    public async Task<IActionResult> Disable(MfaDisableViewModel model, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null) return Unauthorized(ApiResponse.Fail(["Usuário não autenticado."]));

        var result = await authenticationService.DisableMfaAsync(userId.Value, model.Code, model.Password, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<object>.Ok())
            : BadRequest(ApiResponse.Fail(result.Errors));
    }
}

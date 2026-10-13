using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Account;
using InfraControl.Application.Account.Settings.Commands;
using InfraControl.Application.Account.Settings.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Account;

/// <summary>
/// Preferências do usuário autenticado (Fase 06d). Settings são SEMPRE do
/// próprio usuário — o userId vem do cookie de sessão (ICurrentUserContext),
/// nunca do payload. Qualquer role autenticada mexe nas próprias preferências
/// (mesmo contrato do PUT /auth/profile).
/// </summary>
[Route("api/v1/account/settings")]
[Authorize]
public sealed class AccountSettingsController(
    IMediator sender,
    ICurrentUserContext currentUser) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Unauthorized(ApiResponse.Fail(["Usuário não autenticado."]));

        return Ok(ApiResponse.Ok(await sender.Send(
            new GetUserSettingsQuery(userId), cancellationToken)));
    }

    /// <summary>Upsert: cria com defaults e sobrescreve com o payload.</summary>
    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateUserSettingsViewModel model,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Unauthorized(ApiResponse.Fail(["Usuário não autenticado."]));

        var result = await sender.Send(new UpdateUserSettingsCommand(
            userId,
            model.Timezone,
            model.DarkMode,
            model.AutoRefresh,
            model.Telemetry), cancellationToken);

        return Ok(ApiResponse.Ok(result));
    }
}

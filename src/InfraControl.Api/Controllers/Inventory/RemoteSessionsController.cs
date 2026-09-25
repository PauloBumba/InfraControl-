using InfraControl.Application.RemoteAccess.Sessions;
using InfraControl.Api.Controllers;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Inventory;

/// <summary>
/// Histórico de sessões de acesso remoto do device (F11-T001, ADR-013).
/// Somente leitura: abrir/conectar vem com os providers SSH/RDP (F11-T002/T003)
/// e o DTO nunca expõe credenciais (docs/10).
/// </summary>
[Route("api/v1/inventory/devices/{deviceId:guid}/remote/sessions")]
public sealed class RemoteSessionsController(ISender sender) : BaseController
{
    /// <summary>Sessões registradas do device, mais recente primeiro.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetSessions(
        Guid deviceId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(
            await sender.Send(new GetDeviceSessionsQuery(deviceId, limit), cancellationToken)));

    /// <summary>
    /// Abre uma sessão SSH no device (F11-T002). Sem terminal interativo —
    /// a conexão fica registrada até o encerramento.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> OpenSshSession(
        Guid deviceId,
        CancellationToken cancellationToken = default)
        => StatusCode(
            StatusCodes.Status201Created,
            ApiResponse.Ok(await sender.Send(
                new OpenSshSessionCommand(deviceId), cancellationToken)));

    /// <summary>Encerra a sessão SSH ativa e descarta a conexão (F11-T002).</summary>
    [HttpDelete("{sessionId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CloseSshSession(
        Guid deviceId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new CloseSshSessionCommand(deviceId, sessionId), cancellationToken)));
}

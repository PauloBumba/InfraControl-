using InfraControl.Application.RemoteAccess.Sessions;
using InfraControl.Api.Controllers;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

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
    [Authorize]
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
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> OpenSshSession(
        Guid deviceId,
        CancellationToken cancellationToken = default)
        => StatusCode(
            StatusCodes.Status201Created,
            ApiResponse.Ok(await sender.Send(
                new OpenSshSessionCommand(deviceId), cancellationToken)));

    /// <summary>
    /// Abre uma sessão RDP no device via probe TCP/X.224 (F11-T003):
    /// confirma que o endpoint fala RDP, sem autenticação.
    /// </summary>
    [HttpPost("rdp")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> OpenRdpSession(
        Guid deviceId,
        CancellationToken cancellationToken = default)
        => StatusCode(
            StatusCodes.Status201Created,
            ApiResponse.Ok(await sender.Send(
                new OpenRdpSessionCommand(deviceId), cancellationToken)));

    /// <summary>
    /// Encerra a sessão de acesso remoto (SSH/RDP) e descarta a conexão
    /// (F11-T003: comando protocolo-agnóstico).
    /// </summary>
    [HttpDelete("{sessionId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> CloseRemoteSession(
        Guid deviceId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new CloseRemoteSessionCommand(deviceId, sessionId), cancellationToken)));
}

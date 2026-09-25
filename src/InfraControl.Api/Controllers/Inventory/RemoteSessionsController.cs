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
}

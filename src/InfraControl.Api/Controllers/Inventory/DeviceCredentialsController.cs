using InfraControl.Application.RemoteAccess.Credentials;
using InfraControl.Api.Controllers;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Inventory;

/// <summary>
/// Credencial de acesso remoto por device (F11-T006, ADR-013 / docs/10):
/// gestão com CredentialManage (Admin) — a senha nunca aparece em resposta.
/// </summary>
[Route("api/v1/inventory/devices/{deviceId:guid}/remote/credential")]
public sealed class DeviceCredentialsController(ISender sender) : BaseController
{
    /// <summary>Estado da credencial (username e flags — sem senha).</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> GetCredential(
        Guid deviceId,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetDeviceCredentialQuery(deviceId), cancellationToken)));

    /// <summary>
    /// Cadastra ou rotaciona a credencial (PUT substitui e registra
    /// RotatedAt — F11-T006).
    /// </summary>
    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> UpsertCredential(
        Guid deviceId,
        UpsertDeviceCredentialRequest request,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpsertDeviceCredentialCommand(
                deviceId, request.Username, request.Password),
            cancellationToken)));

    /// <summary>
    /// Remove a credencial — o acesso volta ao fallback global
    /// (RemoteAccess:Ssh).
    /// </summary>
    [HttpDelete]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> DeleteCredential(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        await sender.Send(new DeleteDeviceCredentialCommand(deviceId), cancellationToken);
        return Ok(ApiResponse.Ok());
    }
}

/// <summary>Payload do PUT de credencial — senha só via corpo, nunca em URL.</summary>
public sealed record UpsertDeviceCredentialRequest(string Username, string Password);

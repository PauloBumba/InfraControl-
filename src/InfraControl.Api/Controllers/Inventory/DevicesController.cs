using InfraControl.Application.Inventory.Devices.Commands;
using InfraControl.Application.Inventory.Devices.Queries;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Inventory;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CheckKind = InfraControl.Domain.Enums.CheckKind;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Inventory;

/// <summary>
/// F2-T003: CRUD de Devices. Falhas (400/404/409/401/403) são embrulhadas
/// pelo GlobalExceptionHandler — o controller só compõe sucesso no envelope.
/// </summary>
[Route("api/v1/inventory/devices")]
public sealed class DevicesController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? locationId,
        [FromQuery] Guid? rackId,
        [FromQuery] string? type,
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchDevicesQuery(locationId, rackId, type, term, page, pageSize), cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetDeviceQuery(id), cancellationToken)));

    [HttpGet("{id:guid}/status")]
    [Authorize]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetDeviceConsolidatedStatusQuery(id), cancellationToken)));

    // F30-T001 (docs/42 §A): identificação do switch (uptime/sysDescr) lida
    // do histórico de polls — sem coluna nova em Device.
    [HttpGet("{id:guid}/system-info")]
    [Authorize]
    public async Task<IActionResult> GetSystemInfo(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetDeviceSystemInfoQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDeviceViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateDeviceCommand(
                model.LocationId,
                model.Name,
                model.Type,
                model.RackId,
                model.Ip,
                model.Mac,
                model.Serial,
                model.Model,
                model.Vendor,
                model.VlanId,
                model.Description,
                model.Extension,
                model.SipServer,
                model.OperatingSystem),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateDeviceViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateDeviceCommand(
                id,
                model.Name,
                model.Type,
                model.RackId,
                model.Ip,
                model.Mac,
                model.Serial,
                model.Model,
                model.Vendor,
                model.VlanId,
                model.Description,
                model.Extension,
                model.SipServer,
                model.OperatingSystem),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteDeviceCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>F2-T008: override do perfil de checks ou reset para o default do tipo.</summary>
    [HttpPut("{id:guid}/profile")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UpdateProfile(
        Guid id,
        [FromBody] UpdateDeviceProfileViewModel model,
        CancellationToken cancellationToken)
    {
        CheckKind[]? checks = null;
        if (model.Checks is { Length: > 0 })
        {
            checks = new CheckKind[model.Checks.Length];
            for (var i = 0; i < model.Checks.Length; i++)
            {
                if (!Enum.TryParse(model.Checks[i], ignoreCase: true, out CheckKind parsed))
                    throw new ArgumentException(
                        $"Tipo de check inválido: '{model.Checks[i]}'.", nameof(model));
                checks[i] = parsed;
            }
        }

        await sender.Send(
            new UpdateDeviceProfileCommand(id, checks, model.UseDefault),
            cancellationToken);
        return NoContent();
    }
}

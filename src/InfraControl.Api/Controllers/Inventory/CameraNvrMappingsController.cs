using InfraControl.Application.Inventory.CameraNvrMappings.Commands;
using InfraControl.Application.Inventory.CameraNvrMappings.Queries;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Inventory;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Inventory;

/// <summary>
/// F30-T005c: CRUD da relação câmera ↔ NVR (docs/46 §Modelo de domínio).
/// Falhas (400/404/409/401/403) são embrulhadas pelo GlobalExceptionHandler —
/// o controller só compõe sucesso no envelope.
/// </summary>
[Route("api/v1/inventory/camera-nvr-mappings")]
public sealed class CameraNvrMappingsController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? nvrId,
        [FromQuery] Guid? cameraId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchCameraNvrMappingsQuery(nvrId, cameraId, page, pageSize),
            cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetCameraNvrMappingQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCameraNvrMappingViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateCameraNvrMappingCommand(model.CameraId, model.NvrId, model.ChannelNumber),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCameraNvrMappingViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateCameraNvrMappingCommand(id, model.CameraId, model.NvrId, model.ChannelNumber),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteCameraNvrMappingCommand(id), cancellationToken);
        return NoContent();
    }
}

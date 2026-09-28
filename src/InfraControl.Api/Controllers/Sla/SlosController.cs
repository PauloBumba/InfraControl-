using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Sla;
using InfraControl.Application.Sla.Slos.Commands;
using InfraControl.Application.Sla.Slos.Queries;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Sla;

/// <summary>
/// F8-T005: CRUD de SLOs — objetivo de serviço (Availability >= alvo,
/// janela de N dias — docs/04). Falhas embrulhadas pelo
/// GlobalExceptionHandler.
/// </summary>
[Route("api/v1/sla/slos")]
public sealed class SlosController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "SlaRead")]
    public async Task<IActionResult> Search(
        [FromQuery] string? term,
        [FromQuery] Guid? serviceId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchSlosQuery(term, serviceId, page, pageSize), cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "SlaRead")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetSloQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = "SlaManage")]
    public async Task<IActionResult> Create(
        [FromBody] CreateSloViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateSloCommand(
                model.Name, model.TargetPercent, model.WindowDays, model.ServiceId),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SlaManage")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateSloViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateSloCommand(
                id, model.Name, model.TargetPercent, model.WindowDays, model.ServiceId),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SlaManage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteSloCommand(id), cancellationToken);
        return NoContent();
    }
}

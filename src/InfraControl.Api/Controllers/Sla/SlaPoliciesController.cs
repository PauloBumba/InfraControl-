using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Sla;
using InfraControl.Application.Sla.Policies.Commands;
using InfraControl.Application.Sla.Policies.Queries;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Sla;

/// <summary>
/// F8-T002: CRUD de políticas de SLA (compromisso de resposta/resolução).
/// Falhas (400/404/409/401/403) embrulhadas pelo GlobalExceptionHandler.
/// </summary>
[Route("api/v1/sla/policies")]
public sealed class SlaPoliciesController(ISender sender) : BaseController
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
            new SearchSlaPoliciesQuery(term, serviceId, page, pageSize), cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "SlaRead")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetSlaPolicyQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = "SlaManage")]
    public async Task<IActionResult> Create(
        [FromBody] CreateSlaPolicyViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateSlaPolicyCommand(
                model.Name, model.ResponseMinutes, model.ResolutionMinutes, model.ServiceId),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SlaManage")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateSlaPolicyViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateSlaPolicyCommand(
                id, model.Name, model.ResponseMinutes, model.ResolutionMinutes, model.ServiceId),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SlaManage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteSlaPolicyCommand(id), cancellationToken);
        return NoContent();
    }
}

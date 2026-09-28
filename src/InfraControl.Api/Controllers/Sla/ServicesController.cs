using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Sla;
using InfraControl.Application.Sla.Services.Commands;
using InfraControl.Application.Sla.Services.Queries;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Sla;

/// <summary>
/// F8-T001: CRUD de Services. Falhas (400/404/409/401/403) embrulhadas
/// pelo GlobalExceptionHandler; SLA/SLO = F8-T002→T007.
/// </summary>
[Route("api/v1/sla/services")]
public sealed class ServicesController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "SlaRead")]
    public async Task<IActionResult> Search(
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchServicesQuery(term, page, pageSize), cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "SlaRead")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetServiceQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = "SlaManage")]
    public async Task<IActionResult> Create(
        [FromBody] CreateServiceViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateServiceCommand(model.Name, model.Description),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SlaManage")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateServiceViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateServiceCommand(id, model.Name, model.Description),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SlaManage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteServiceCommand(id), cancellationToken);
        return NoContent();
    }
}

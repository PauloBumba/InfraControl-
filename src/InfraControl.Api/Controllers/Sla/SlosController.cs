using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Sla;
using InfraControl.Application.Sla.Slos.Budgets.Commands;
using InfraControl.Application.Sla.Slos.Budgets.Queries;
using InfraControl.Application.Sla.Slos.Commands;
using InfraControl.Application.Sla.Slos.Measurements.Commands;
using InfraControl.Application.Sla.Slos.Measurements.Queries;
using InfraControl.Application.Sla.Slos.Queries;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

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
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? term,
        [FromQuery] Guid? serviceId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchSlosQuery(term, serviceId, page, pageSize), cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetSloQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
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
    [Authorize(Policy = AuthorizationPolicies.Operate)]
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
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteSloCommand(id), cancellationToken);
        return NoContent();
    }

    // F8-T006: medições do SLO — registro imutável (sem PUT), 404 para
    // SLO/medição inexistente.

    [HttpGet("{sloId:guid}/measurements")]
    [Authorize]
    public async Task<IActionResult> SearchMeasurements(
        Guid sloId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchSloMeasurementsQuery(sloId, page, pageSize), cancellationToken)));

    [HttpGet("measurements/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetMeasurement(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetSloMeasurementQuery(id), cancellationToken)));

    [HttpPost("{sloId:guid}/measurements")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> RecordMeasurement(
        Guid sloId,
        [FromBody] RecordSloMeasurementViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RecordSloMeasurementCommand(
                sloId, model.MeasuredAt, model.ValuePercent),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpDelete("measurements/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DeleteMeasurement(
        Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteSloMeasurementCommand(id), cancellationToken);
        return NoContent();
    }

    // F8-T007: orçamento (erro budget) do SLO — mesmo desenho das medições.

    [HttpGet("{sloId:guid}/budgets")]
    [Authorize]
    public async Task<IActionResult> SearchBudgets(
        Guid sloId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchSloBudgetsQuery(sloId, page, pageSize), cancellationToken)));

    [HttpGet("budgets/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetBudget(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetSloBudgetQuery(id), cancellationToken)));

    [HttpPost("{sloId:guid}/budgets")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> RecordBudget(
        Guid sloId,
        [FromBody] RecordSloBudgetViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RecordSloBudgetCommand(
                sloId, model.PeriodStart, model.PeriodEnd, model.ConsumedPercent),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpDelete("budgets/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DeleteBudget(
        Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteSloBudgetCommand(id), cancellationToken);
        return NoContent();
    }
}

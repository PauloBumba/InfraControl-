using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Tasks;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Tasks;

/// <summary>
/// F7-T001: CRUD de Tasks (docs/04). Falhas (400/404/401/403) embrulhadas
/// pelo GlobalExceptionHandler; estado/transição = F7-T007 (Workflow).
/// </summary>
[Route("api/v1/tasks")]
public sealed class TasksController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "TasksRead")]
    public async Task<IActionResult> Search(
        [FromQuery] string? term,
        [FromQuery] string? kind,
        [FromQuery] string? state,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchTasksQuery(term, kind, state, page, pageSize),
            cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "TasksRead")]
    public async Task<IActionResult> GetById(
        Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetTaskQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = "TasksManage")]
    public async Task<IActionResult> Create(
        [FromBody] CreateTaskViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateTaskCommand(model.Title, model.Kind, model.Description),
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "TasksManage")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTaskViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateTaskCommand(id, model.Title, model.Description),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "TasksManage")]
    public async Task<IActionResult> Delete(
        Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteTaskCommand(id), cancellationToken);
        return NoContent();
    }
}

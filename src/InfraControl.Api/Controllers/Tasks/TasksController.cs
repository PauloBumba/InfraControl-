using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Sla;
using InfraControl.Api.ViewModels.Tasks;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Application.Sla.TaskSlas.Commands;
using InfraControl.Application.Sla.TaskSlas.Queries;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.History;
using InfraControl.Application.Tasks.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Tasks;

/// <summary>
/// F7-T001: CRUD de Tasks (docs/04). Falhas (400/404/401/403) embrulhadas
/// pelo GlobalExceptionHandler; estado/transição = F7-T007 (Workflow).
/// </summary>
[Route("api/v1/tasks")]
public sealed class TasksController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize]
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
    [Authorize]
    public async Task<IActionResult> GetById(
        Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetTaskQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
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
    [Authorize(Policy = AuthorizationPolicies.Operate)]
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
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Delete(
        Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteTaskCommand(id), cancellationToken);
        return NoContent();
    }

    // F7-T007 (Workflow): transição de estado — mapa no domínio
    // (409 fora do mapa), 400 estado desconhecido; mesmo estado = no-op
    // sem histórico. Retorna o TaskDto atualizado.

    [HttpPut("{id:guid}/state")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> ChangeState(
        Guid id,
        [FromBody] ChangeTaskStateViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new ChangeTaskStateCommand(id, model.State), cancellationToken)));


    // F7-T002: responsáveis (N:N com usuários) — 204 nos dois sentidos
    // e estado atual embutido no TaskDto (assigneeIds).

    [HttpPost("{taskId:guid}/assignees/{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Assign(
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new AssignTaskCommand(taskId, userId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{taskId:guid}/assignees/{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Unassign(
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UnassignTaskCommand(taskId, userId), cancellationToken);
        return NoContent();
    }

    // F7-T003: devices vinculados (N:N) — 204 nos dois sentidos e estado
    // atual embutido no TaskDto (deviceIds).

    [HttpPost("{taskId:guid}/devices/{deviceId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> LinkDevice(
        Guid taskId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new LinkTaskDeviceCommand(taskId, deviceId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{taskId:guid}/devices/{deviceId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UnlinkDevice(
        Guid taskId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UnlinkTaskDeviceCommand(taskId, deviceId), cancellationToken);
        return NoContent();
    }

    // F7-T004: incidentes vinculados (N:N) — 204 nos dois sentidos e estado
    // atual embutido no TaskDto (incidentIds).

    [HttpPost("{taskId:guid}/incidents/{incidentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> LinkIncident(
        Guid taskId,
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new LinkTaskIncidentCommand(taskId, incidentId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{taskId:guid}/incidents/{incidentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UnlinkIncident(
        Guid taskId,
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UnlinkTaskIncidentCommand(taskId, incidentId), cancellationToken);
        return NoContent();
    }

    // F7-T005: comentários — autor é o usuário logado (ICurrentUserContext).

    [HttpGet("{taskId:guid}/comments")]
    [Authorize]
    public async Task<IActionResult> GetComments(
        Guid taskId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTaskCommentsQuery(taskId), cancellationToken)));

    [HttpGet("{taskId:guid}/history")]
    [Authorize]
    public async Task<IActionResult> GetHistory(
        Guid taskId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTaskHistoryQuery(taskId), cancellationToken)));

    [HttpPost("{taskId:guid}/comments")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> AddComment(
        Guid taskId,
        [FromBody] AddTaskCommentViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AddTaskCommentCommand(taskId, model.Content), cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    // F8-T003: SLA aplicado — snapshot dos prazos (docs/04); 409 se já
    // aplicado, 404 task/política.

    [HttpGet("{taskId:guid}/sla")]
    [Authorize]
    public async Task<IActionResult> GetSla(
        Guid taskId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTaskSlaQuery(taskId), cancellationToken)));

    [HttpPost("{taskId:guid}/sla")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> ApplySla(
        Guid taskId,
        [FromBody] ApplyTaskSlaViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ApplyTaskSlaCommand(taskId, model.SlaPolicyId), cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    // F8-T004: pausas do relógio de SLA — pausa/resume estendem os
    // vencimentos ao fechar (snapshot de minutos intacto).

    [HttpGet("{taskId:guid}/sla/pauses")]
    [Authorize]
    public async Task<IActionResult> GetSlaPauses(
        Guid taskId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTaskSlaPausesQuery(taskId), cancellationToken)));

    [HttpPost("{taskId:guid}/sla/pause")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> PauseSla(
        Guid taskId, CancellationToken cancellationToken)
    {
        await sender.Send(new PauseTaskSlaCommand(taskId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{taskId:guid}/sla/resume")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> ResumeSla(
        Guid taskId, CancellationToken cancellationToken)
    {
        await sender.Send(new ResumeTaskSlaCommand(taskId), cancellationToken);
        return NoContent();
    }
}

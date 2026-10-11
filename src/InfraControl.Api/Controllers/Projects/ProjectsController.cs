using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Projects;
using InfraControl.Application.Knowledge.Articles.Commands;
using InfraControl.Application.Projects.Commands;
using InfraControl.Application.Projects.Queries;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using InfraControl.Api.Services;

namespace InfraControl.Api.Controllers.Projects;

/// <summary>
/// Governança do ciclo de vida dos projetos: submissão para homologação e
/// aprovação formal exclusiva por Administrador.
/// </summary>
[Route("api/v1/projects")]
public sealed class ProjectsController(
    ISender sender,
    IOptions<FrontendOptions> frontendOptions) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? term,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] Guid? leaderId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchProjectsQuery(term, status, priority, leaderId, page, pageSize),
            cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetProjectQuery(id),
            cancellationToken)));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProjectViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateProjectCommand(
                model.Code,
                model.Title,
                model.Description,
                model.LeaderId,
                model.Priority,
                model.Justification,
                model.ManagerId,
                model.PlannedStartDate,
                model.PlannedEndDate),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPost("{id:guid}/invitations")]
    [Authorize]
    public async Task<IActionResult> CreateInvitation(
        Guid id,
        [FromBody] CreateProjectInvitationViewModel model,
        CancellationToken cancellationToken)
    {
        var configured = frontendOptions.Value.BaseUrl;
        var baseUrl = string.IsNullOrWhiteSpace(configured)
            ? $"{Request.Scheme}://{Request.Host}"
            : configured.TrimEnd('/');

        var result = await sender.Send(
            new CreateProjectInvitationCommand(
                id,
                model.Email,
                model.Role,
                baseUrl,
                model.ExpiresInDays),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProjectViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateProjectCommand(
                id,
                model.Title,
                model.Description,
                model.Justification,
                model.Priority,
                model.LeaderId,
                model.ManagerId,
                model.PlannedStartDate,
                model.PlannedEndDate),
            cancellationToken);

        return Ok(ApiResponse.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteProjectCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        [FromBody] ProjectStatusViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new TransitionProjectStatusCommand(id, model.Status, model.Reason),
            cancellationToken)));

    [HttpGet("{id:guid}/phases")]
    [Authorize]
    public async Task<IActionResult> GetPhases(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetProjectPhasesQuery(id),
            cancellationToken)));

    [HttpPost("{id:guid}/phases")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> AddPhase(
        Guid id,
        [FromBody] CreateProjectPhaseViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new AddProjectPhaseCommand(
                id,
                model.Title,
                model.Description,
                model.CompletionCriteria,
                model.ResponsibleId,
                model.PlannedStartDate,
                model.PlannedEndDate),
            cancellationToken)));

    [HttpPut("{id:guid}/phases/{phaseId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UpdatePhase(
        Guid id,
        Guid phaseId,
        [FromBody] UpdateProjectPhaseViewModel model,
        CancellationToken cancellationToken)
    {
        _ = id;
        var result = await sender.Send(
            new UpdateProjectPhaseCommand(
                phaseId,
                model.Title,
                model.Description,
                model.CompletionCriteria,
                model.ResponsibleId,
                model.PlannedStartDate,
                model.PlannedEndDate,
                model.Percent),
            cancellationToken);

        return Ok(ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}/phases/reorder")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> ReorderPhases(
        Guid id,
        [FromBody] ReorderProjectPhasesViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new ReorderProjectPhasesCommand(id, model.PhaseIdsInOrder),
            cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/phases/{phaseId:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> SetPhaseStatus(
        Guid id,
        Guid phaseId,
        [FromBody] SetProjectPhaseStatusViewModel model,
        CancellationToken cancellationToken)
    {
        _ = id;
        var result = await sender.Send(
            new SetPhaseStatusCommand(phaseId, model.Action, model.Reason),
            cancellationToken);

        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("{id:guid}/submit-approval")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> SubmitForApproval(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SubmitProjectForApprovalCommand(id),
            cancellationToken)));

    [HttpPost("{id:guid}/generate-knowledge")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GenerateKnowledge(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GenerateProjectKnowledgeCommand(id),
            cancellationToken)));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ApproveProjectViewModel model,
        CancellationToken cancellationToken)
    {
        model ??= new ApproveProjectViewModel();

        return Ok(ApiResponse.Ok(await sender.Send(
            new ApproveProjectCommand(id, model.ApprovalNotes),
            cancellationToken)));
    }
}

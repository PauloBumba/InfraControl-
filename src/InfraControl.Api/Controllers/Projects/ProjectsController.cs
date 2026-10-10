using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Projects;
using InfraControl.Application.Knowledge.Articles.Commands;
using InfraControl.Application.Projects.Commands;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Projects;

/// <summary>
/// Governança do ciclo de vida dos projetos: submissão para homologação e
/// aprovação formal exclusiva por Administrador.
/// </summary>
[Route("api/v1/projects")]
public sealed class ProjectsController(ISender sender) : BaseController
{
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

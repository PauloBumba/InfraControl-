using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Knowledge;
using InfraControl.Application.Knowledge.Categories;
using InfraControl.Application.Knowledge.Categories.Commands;
using InfraControl.Application.Knowledge.Categories.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Knowledge;

/// <summary>
/// F10-T001: CRUD de categorias da base de conhecimento (docs/04).
/// Falhas embrulhadas pelo GlobalExceptionHandler.
/// </summary>
[Route("api/v1/knowledge/categories")]
public sealed class KnowledgeController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchKnowledgeCategoriesQuery(term, page, pageSize), cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetKnowledgeCategoryQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Create(
        [FromBody] CreateKnowledgeCategoryViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateKnowledgeCategoryCommand(model.Name, model.Description),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateKnowledgeCategoryViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateKnowledgeCategoryCommand(id, model.Name, model.Description),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteKnowledgeCategoryCommand(id), cancellationToken);
        return NoContent();
    }
}

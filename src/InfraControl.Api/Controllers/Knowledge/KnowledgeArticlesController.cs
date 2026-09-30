using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Knowledge;
using InfraControl.Application.Knowledge.Articles;
using InfraControl.Application.Knowledge.Articles.Commands;
using InfraControl.Application.Knowledge.Articles.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Knowledge;

/// <summary>
/// F10-T002: CRUD de artigos da base de conhecimento (docs/04).
/// Controller próprio — o de categorias (F10-T001) já ocupa a rota
/// api/v1/knowledge/categories. Transições de status vêm em F10-T003.
/// Falhas embrulhadas pelo GlobalExceptionHandler.
/// </summary>
[Route("api/v1/knowledge/articles")]
public sealed class KnowledgeArticlesController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchKnowledgeArticlesQuery(term, page, pageSize), cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetKnowledgeArticleQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Create(
        [FromBody] CreateKnowledgeArticleViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateKnowledgeArticleCommand(model.Title, model.Content, model.CategoryId),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateKnowledgeArticleViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateKnowledgeArticleCommand(id, model.Title, model.Content, model.CategoryId),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteKnowledgeArticleCommand(id), cancellationToken);
        return NoContent();
    }
}

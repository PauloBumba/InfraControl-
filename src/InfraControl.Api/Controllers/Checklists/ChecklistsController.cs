using InfraControl.Api.Authorization;
using InfraControl.Api.ViewModels.Checklists;
using InfraControl.Application.Checklists.Commands;
using InfraControl.Application.Checklists.Queries;

namespace InfraControl.Api.Controllers.Checklists;

/// <summary>
/// F50-T007/ADR-067: checklist genérico — itens por dono polimórfico
/// (device/location/incident/project). O tipo do dono é string na rota
/// e validado no handler (400 pt-BR com as opções); dono/item
/// inexistente = 404 pt-BR. Sem timeline própria (ADR-067 §6).
/// </summary>
[Route("api/v1/checklists")]
public sealed class ChecklistsController(ISender sender) : BaseController
{
    [HttpGet("{ownerType}/{ownerId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetItems(
        string ownerType,
        Guid ownerId,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetChecklistItemsQuery(ownerType, ownerId), cancellationToken)));

    [HttpPost("{ownerType}/{ownerId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> AddItem(
        string ownerType,
        Guid ownerId,
        [FromBody] AddChecklistItemViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AddChecklistItemCommand(ownerType, ownerId, model.Description),
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("items/{itemId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UpdateItem(
        Guid itemId,
        [FromBody] UpdateChecklistItemViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpdateChecklistItemCommand(
                itemId, model.Description, model.Done),
            cancellationToken)));

    [HttpDelete("items/{itemId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DeleteItem(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteChecklistItemCommand(itemId), cancellationToken);
        return NoContent();
    }
}

using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Admin;
using InfraControl.Application.AI.Providers.Commands;
using InfraControl.Application.AI.Providers.Queries;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Admin;

/// <summary>
/// FAI-T011b (ADR-057): CRUD Administer do cadastro de providers de IA e
/// do roteamento capacidade → provider. Falhas (400/404/401/403) são
/// embrulhadas pelo GlobalExceptionHandler.
/// </summary>
[Route("api/v1/admin/ai-providers")]
public sealed class AiProvidersController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Search(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(
            await sender.Send(new SearchAiProvidersQuery(), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAiProviderViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateAiProviderCommand(
                model.Name, model.BaseUrl, model.ApiKey, model.Models),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAiProviderViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpdateAiProviderCommand(id, model.BaseUrl, model.ApiKey, model.Models),
            cancellationToken)));

    [HttpPut("{id:guid}/active")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> SetActive(
        Guid id,
        [FromBody] SetAiProviderActiveViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SetAiProviderActiveCommand(id, model.Active), cancellationToken)));

    [HttpPut("capabilities/{capability}")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> UpsertCapability(
        string capability,
        [FromBody] UpsertAiProviderCapabilityViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpsertAiProviderCapabilityRouteCommand(
                capability,
                model.ProviderId,
                model.DefaultModel,
                model.FallbackProviderId,
                model.FallbackModel),
            cancellationToken)));

    [HttpDelete("capabilities/{capability}")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> DeleteCapability(
        string capability,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new DeleteAiProviderCapabilityRouteCommand(capability), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/test-connection")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> TestConnection(
        Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new TestAiProviderConnectionCommand(id), cancellationToken)));

    [HttpGet("{id:guid}/models")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> ListModels(
        Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new ListAiProviderModelsCommand(id), cancellationToken)));
}

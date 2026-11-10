using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Admin;
using InfraControl.Application.AI.Providers.Commands;
using InfraControl.Application.AI.Providers.Queries;
using InfraControl.Application.AI.Validation;
using InfraControl.Application.AI.Validation.Commands;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Admin;

/// <summary>
/// F49-T004: entrada do dry-run de validação (protocolo por nome
/// "openai-compatible"|"ollama"|"gemini"|"anthropic" ou número).
/// </summary>
public sealed record ValidateAiConnectionRequest(
    string Protocol,
    string BaseUrl,
    string? ApiKey = null,
    string? Model = null);

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
    {
        InfraControl.Domain.Enums.AiProviderProtocol? protocol = null;
        if (!string.IsNullOrWhiteSpace(model.Protocol))
        {
            if (!Enum.TryParse<InfraControl.Domain.Enums.AiProviderProtocol>(
                    model.Protocol, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                throw new ArgumentException(
                    $"Protocolo desconhecido '{model.Protocol}' — use openai-compatible, ollama, gemini ou anthropic.",
                    nameof(model));
            }
            protocol = parsed;
        }

        return Ok(ApiResponse.Ok(await sender.Send(
            new UpdateAiProviderCommand(
                id, model.BaseUrl, model.ApiKey, model.Models, protocol),
            cancellationToken)));
    }

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

    // F49-T004: validação em fases (dry-run) de um endpoint arbitrário
    // — nunca persiste; key via body write-only e devolvida só como
    // status/fase (§44/ADR-057).
    [HttpPost("validate")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> Validate(
        [FromBody] ValidateAiConnectionRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AiProtocol>(request.Protocol, ignoreCase: true, out var protocol)
            || !Enum.IsDefined(protocol))
        {
            throw new ArgumentException(
                $"Protocolo desconhecido '{request.Protocol}' — use openai-compatible, ollama, gemini ou anthropic.",
                nameof(request));
        }

        return Ok(ApiResponse.Ok(await sender.Send(
            new ValidateAiConnectionCommand(
                protocol, request.BaseUrl, request.ApiKey, request.Model),
            cancellationToken)));
    }

    [HttpGet("{id:guid}/models")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> ListModels(
        Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new ListAiProviderModelsCommand(id), cancellationToken)));
}

using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.AI.Knowledge;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.AI;

/// <summary>
/// Knowledge Engineer (ADR-054 · Fase D) — rascunho de artigo com IA a
/// partir de um incidente. Operate; o artigo nasce <c>Draft</c>
/// (workflow ADR-034 intacto) — publicação é transição humana.
/// Sem provider configurado o handler propaga
/// <c>AiProviderNotConfigured</c> → 503 amigável (§44/§57).
/// </summary>
[Route("api/v1/incidents")]
public sealed class IncidentKnowledgeController(ISender sender) : BaseController
{
    /// <summary>
    /// Gera rascunho (Draft) de artigo de knowledge com IA a partir do
    /// incidente, com origem Incident vinculada (K4).
    /// </summary>
    [HttpPost("{id:guid}/generate-knowledge")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GenerateKnowledge(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GenerateIncidentKnowledgeDraftCommand(id), cancellationToken)));
}

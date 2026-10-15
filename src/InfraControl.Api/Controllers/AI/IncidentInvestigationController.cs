using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.AI.Investigation;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.AI;

/// <summary>
/// AI Investigation (ADR-053 · Fase C) — enriquecimento de incidente
/// com análise grounded. Operate (ADR-030 §3): ação operacional; a
/// análise é LEVEL 0 (ADR-040 §1). Sem provider configurado o handler
/// propaga <c>AiProviderNotConfigured</c> → 503 amigável (§44/§57).
/// </summary>
[Route("api/v1/incidents")]
public sealed class IncidentInvestigationController(ISender sender) : BaseController
{
    /// <summary>
    /// Investiga o incidente com IA: dados do incidente + knowledge
    /// recuperado → análise com hipóteses e evidências citadas.
    /// </summary>
    [HttpPost("{id:guid}/investigate")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Investigate(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new InvestigateIncidentCommand(id), cancellationToken)));
}

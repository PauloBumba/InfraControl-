using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.AI.Automation;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.AI;

/// <summary>
/// Automation Agent em LEVEL 1 (ADR-055 · Fase F) — recomendações para
/// o humano decidir; sem escrita, sem aprovação, sem execução (blueprint
/// §39). Operate. Sem provider configurado o handler propaga
/// <c>AiProviderNotConfigured</c> → 503 amigável (§44/§57).
/// </summary>
[Route("api/v1/automation")]
public sealed class AutomationRecommendationsController(ISender sender) : BaseController
{
    /// <summary>
    /// Gera recomendações (LEVEL 1) a partir dos sinais ativos
    /// (incidentes) + knowledge; sem sinais, lista vazia sem chamar a IA.
    /// </summary>
    [HttpPost("recommendations")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Recommend(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GenerateAutomationRecommendationsCommand(), cancellationToken)));
}

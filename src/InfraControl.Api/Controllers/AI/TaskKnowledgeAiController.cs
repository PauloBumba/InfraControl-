using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.AI.Knowledge;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.AI;

/// <summary>
/// Knowledge Engineer (ADR-054 · Fase D) — variante com IA da geração de
/// rascunho a partir de task concluída (irmã da rota determinística
/// <c>/generate-knowledge</c>). Operate; nasce <c>Draft</c> — publicação é
/// transição humana (ADR-034).
/// </summary>
[Route("api/v1/tasks")]
public sealed class TaskKnowledgeAiController(ISender sender) : BaseController
{
    /// <summary>
    /// Gera rascunho (Draft) de artigo de knowledge com IA a partir da
    /// task concluída (Resolved/Closed), com origem Task vinculada (K4).
    /// </summary>
    [HttpPost("{taskId:guid}/generate-knowledge/ai")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GenerateKnowledgeWithAi(
        Guid taskId,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GenerateTaskKnowledgeDraftCommand(taskId), cancellationToken)));
}

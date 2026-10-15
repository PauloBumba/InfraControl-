using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.AI.Chat;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.AI;

/// <summary>
/// Copilot (ADR-052 · Fase B) — chat grounded LEVEL 0. Read (§41):
/// qualquer role autenticada conversa; a IA enxerga o que o usuário
/// enxerga (ADR-040 §3). Sem provider configurado o handler propaga
/// <c>AiProviderNotConfigured</c> → 503 amigável (§44/§57).
/// </summary>
[Route("api/v1/ai")]
public sealed class AiChatController(ISender sender) : BaseController
{
    /// <summary>
    /// Converse com o Copilot. Envia a conversa (últimas 20 turns; a
    /// última é do usuário) e recebe resposta grounded + evidências.
    /// </summary>
    [HttpPost("chat")]
    [Authorize]
    public async Task<IActionResult> Chat(
        [FromBody] ChatRequest body,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SendAiChatCommand(body.Messages), cancellationToken)));
}

/// <summary>Envelope do request de chat (a API devolve <c>data</c> tipado).</summary>
public sealed record ChatRequest(IReadOnlyList<ChatTurn> Messages);

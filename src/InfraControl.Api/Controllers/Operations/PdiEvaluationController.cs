using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Pdi;
using InfraControl.Application.Pdi.Commands;
using InfraControl.Application.Pdi.Queries;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Operations;

/// <summary>
/// Avaliação do PDI (docs/30 §15.3–§16, ADR-037) — os handlers já
/// existiam no Application, mas nenhum controller expunha as rotas que o
/// front invocava (PdiEvaluationDialog) — expostas aqui.
/// Self-service do ADR-030: o dono grava a autoavaliação, o gestor/Admin
/// a avaliação do gestor; o **lado é determinado no servidor**
/// (PdiAccess.EnsureCanEvaluate). Operate nas três rotas — o fine-grained
/// fica no handler (403 pt-BR).
/// </summary>
[Route("api/v1/pdi")]
public sealed class PdiEvaluationController(ISender sender) : BaseController
{
    /// <summary>Avaliação final: plano + as duas avaliações + resultado (§16).</summary>
    [HttpGet("{userId:guid}/evaluation")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetEvaluation(
        Guid userId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetPdiEvaluationQuery(userId), cancellationToken)));

    /// <summary>
    /// Salva a avaliação do lado do ator (§15.3) — lado no servidor.
    /// </summary>
    [HttpPut("{userId:guid}/assessment")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> SaveAssessment(
        Guid userId,
        [FromBody] SavePdiAssessmentViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SavePdiAssessmentCommand(userId, model.Content),
            cancellationToken)));

    /// <summary>
    /// Resultado final (§16) — único por plano (2ª → 409); Admin/gestor.
    /// </summary>
    [HttpPut("{userId:guid}/result")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> FinalizeResult(
        Guid userId,
        [FromBody] FinalizePdiResultViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new FinalizePdiResultCommand(userId, model.Outcome, model.Notes),
            cancellationToken)));
}

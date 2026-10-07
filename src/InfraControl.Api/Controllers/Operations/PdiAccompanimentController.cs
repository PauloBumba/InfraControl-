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
/// Acompanhamento bilateral da fase (docs/30 §9-§11, §13, Fase 3,
/// ADR-033): progresso/comentários, check-ins (relato do colaborador +
/// resposta do gestor), impedimentos e feedbacks. Operate no controller
/// (feedback DELETE = Administer); dono/gestor/Admin no handler (403);
/// falhas embrulhadas pelo GlobalExceptionHandler.
/// </summary>
[Route("api/v1/pdi")]
public sealed class PdiAccompanimentController(ISender sender) : BaseController
{
    // ---- Leitura ---------------------------------------------------

    /// <summary>
    /// Progresso, check-ins, impedimentos e feedbacks do plano.
    /// Admin: qualquer usuário; dono/gestor: o próprio (403).
    /// </summary>
    [HttpGet("{userId:guid}/accompaniment")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetAccompaniment(
        Guid userId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetPdiAccompanimentQuery(userId), cancellationToken)));

    // ---- Progresso (append-only) -----------------------------------

    /// <summary>Registra progresso/comentário do colaborador na fase.</summary>
    [HttpPost("progresses")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> CreateProgress(
        [FromBody] CreatePdiProgressViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new CreatePdiProgressCommand(
                model.PhaseId, model.Percent, model.Comment),
            cancellationToken)));

    /// <summary>Remove registro de progresso (folha, sem children).</summary>
    [HttpDelete("progresses/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DeleteProgress(
        Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeletePdiProgressCommand(id), cancellationToken);
        return NoContent();
    }

    // ---- Check-ins (§10 bilateral) ---------------------------------

    /// <summary>Cria check-in com o relato do colaborador.</summary>
    [HttpPost("checkins")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> CreateCheckIn(
        [FromBody] CreatePdiCheckInViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new CreatePdiCheckInCommand(
                model.PhaseId, model.CheckInDate, model.WhatDone,
                model.WhatLearned, model.Difficulty, model.NeedSupport),
            cancellationToken)));

    /// <summary>
    /// Edita o check-in por lado: relato (dono/Admin) e/ou resposta
    /// (gestor/Admin). Campos não enviados permanecem intactos.
    /// </summary>
    [HttpPut("checkins/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UpdateCheckIn(
        Guid id,
        [FromBody] UpdatePdiCheckInViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdatePdiCheckInCommand(
                id, model.WhatDone, model.WhatLearned, model.Difficulty,
                model.NeedSupport, model.ManagerFeedback, model.NextAction),
            cancellationToken);
        return NoContent();
    }

    /// <summary>Remove check-in (folha).</summary>
    [HttpDelete("checkins/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DeleteCheckIn(
        Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeletePdiCheckInCommand(id), cancellationToken);
        return NoContent();
    }

    // ---- Impedimentos (§11) ----------------------------------------

    /// <summary>Registra dificuldade/impedimento (status inicial Open).</summary>
    [HttpPost("impediments")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> CreateImpediment(
        [FromBody] CreatePdiImpedimentViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new CreatePdiImpedimentCommand(
                model.PhaseId, model.Type, model.Description, model.OccurredAt,
                model.Impact, model.ResponsibleId, model.ActionTaken),
            cancellationToken)));

    /// <summary>
    /// Atualiza impedimento (ação tomada, status Open/Resolved —
    /// resolver preenche ResolvedAt, reabrir limpa).
    /// </summary>
    [HttpPut("impediments/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UpdateImpediment(
        Guid id,
        [FromBody] UpdatePdiImpedimentViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdatePdiImpedimentCommand(
                id, model.Type, model.Description, model.OccurredAt,
                model.Impact, model.ResponsibleId, model.ActionTaken, model.Status),
            cancellationToken);
        return NoContent();
    }

    /// <summary>Remove impedimento (folha).</summary>
    [HttpDelete("impediments/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DeleteImpediment(
        Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeletePdiImpedimentCommand(id), cancellationToken);
        return NoContent();
    }

    // ---- Feedbacks (§13 append-only) -------------------------------

    /// <summary>
    /// Cria feedback (destinatário = dono ou gestor do plano; autor =
    /// usuário atual). Sem PUT — histórico não se sobrescreve.
    /// </summary>
    [HttpPost("feedbacks")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> CreateFeedback(
        [FromBody] CreatePdiFeedbackViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new CreatePdiFeedbackCommand(
                model.PhaseId, model.RecipientId, model.Type, model.Content,
                model.FeedbackDate),
            cancellationToken)));

    /// <summary>Remove feedback (histórico — só Admin).</summary>
    [HttpDelete("feedbacks/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> DeleteFeedback(
        Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeletePdiFeedbackCommand(id), cancellationToken);
        return NoContent();
    }
}

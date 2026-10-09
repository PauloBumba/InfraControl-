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
/// Estrutura do desenvolvimento (docs/30 §5-§8, Fase 2, ADR-032):
/// competências (catálogo Admin) + árvore objetivo → estratégia →
/// fase → atividade; aprovação de fase (concluir → validar/rejeitar,
/// Fase 5, ADR-035). Operate no controller; dono do plano ou Admin no
/// handler (403); exclusão com children → 409; falhas embrulhadas pelo
/// GlobalExceptionHandler.
/// </summary>
[Route("api/v1/pdi")]
public sealed class PdiStructureController(ISender sender) : BaseController
{
    // ---- Catálogo de competências ---------------------------------

    /// <summary>Catálogo de competências (leitura Operate; ordenado por nome).</summary>
    [HttpGet("competencies")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> ListCompetencies(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(
            await sender.Send(new ListPdiCompetenciesQuery(), cancellationToken)));

    /// <summary>Cria competência no catálogo (nome único → 409).</summary>
    [HttpPost("competencies")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> CreateCompetency(
        [FromBody] PdiCompetencyViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new CreatePdiCompetencyCommand(model.Name, model.Description),
            cancellationToken)));

    /// <summary>Atualiza competência do catálogo (nome único → 409).</summary>
    [HttpPut("competencies/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> UpdateCompetency(
        Guid id,
        [FromBody] PdiCompetencyViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdatePdiCompetencyCommand(id, model.Name, model.Description),
            cancellationToken);
        return NoContent();
    }

    /// <summary>Remove competência (em uso → 409).</summary>
    [HttpDelete("competencies/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> DeleteCompetency(
        Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeletePdiCompetencyCommand(id), cancellationToken);
        return NoContent();
    }

    // ---- Árvore ---------------------------------------------------

    /// <summary>
    /// Árvore objetivos → estratégias → fases → atividades do PDI.
    /// Admin: qualquer usuário; Operador: só o próprio (403).
    /// </summary>
    [HttpGet("{userId:guid}/structure")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetStructure(
        Guid userId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetPdiStructureQuery(userId), cancellationToken)));

    // ---- Objetivos ------------------------------------------------

    /// <summary>Cria objetivo no PDI do usuário (plano inexistente → 404).</summary>
    [HttpPost("objectives")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> CreateObjective(
        [FromBody] CreatePdiObjectiveViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new CreatePdiObjectiveCommand(
                model.UserId, model.Title, model.Description, model.CompetencyId,
                model.CurrentSituation, model.DesiredSituation, model.ExpectedResult,
                model.Indicator, model.TargetDate),
            cancellationToken)));

    /// <summary>Atualiza objetivo (competência inexistente → 400).</summary>
    [HttpPut("objectives/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UpdateObjective(
        Guid id,
        [FromBody] UpdatePdiObjectiveViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdatePdiObjectiveCommand(
                id, model.Title, model.Description, model.CompetencyId,
                model.CurrentSituation, model.DesiredSituation, model.ExpectedResult,
                model.Indicator, model.TargetDate),
            cancellationToken);
        return NoContent();
    }

    /// <summary>Remove objetivo (com estratégias → 409).</summary>
    [HttpDelete("objectives/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DeleteObjective(
        Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeletePdiObjectiveCommand(id), cancellationToken);
        return NoContent();
    }

    // ---- Estratégias ----------------------------------------------

    /// <summary>Cria estratégia no objetivo (o body traz o ObjectiveId).</summary>
    [HttpPost("strategies")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> CreateStrategy(
        [FromBody] CreatePdiStrategyViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new CreatePdiStrategyCommand(
                model.ObjectiveId, model.Title, model.Description),
            cancellationToken)));

    /// <summary>Atualiza estratégia.</summary>
    [HttpPut("strategies/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UpdateStrategy(
        Guid id,
        [FromBody] UpdatePdiStrategyViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdatePdiStrategyCommand(id, model.Title, model.Description),
            cancellationToken);
        return NoContent();
    }

    /// <summary>Remove estratégia (com fases → 409).</summary>
    [HttpDelete("strategies/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DeleteStrategy(
        Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeletePdiStrategyCommand(id), cancellationToken);
        return NoContent();
    }

    // ---- Fases ----------------------------------------------------

    /// <summary>Cria fase na estratégia (status case-insensitive).</summary>
    [HttpPost("phases")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> CreatePhase(
        [FromBody] CreatePdiPhaseViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new CreatePdiPhaseCommand(
                model.StrategyId, model.Title, model.Description,
                model.ResponsibleId, model.StartDate, model.EndDate,
                model.CompletionCriteria, model.Status, model.Percent),
            cancellationToken)));

    /// <summary>Atualiza fase (percentual 0-100; fim ≥ início).</summary>
    [HttpPut("phases/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UpdatePhase(
        Guid id,
        [FromBody] UpdatePdiPhaseViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdatePdiPhaseCommand(
                id, model.Title, model.Description, model.ResponsibleId,
                model.StartDate, model.EndDate, model.CompletionCriteria,
                model.Status, model.Percent),
            cancellationToken);
        return NoContent();
    }

    /// <summary>Remove fase (com atividades → 409).</summary>
    [HttpDelete("phases/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DeletePhase(
        Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeletePdiPhaseCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Conclui a fase e envia para validação do gestor (§15 — Fase 5).
    /// </summary>
    [HttpPost("phases/{id:guid}/submit")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> SubmitPhase(
        Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new SubmitPdiPhaseCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Valida ou rejeita a fase concluída (§15 — Fase 5); rejeição com
    /// motivo obrigatório (400 pt-BR) volta a fase para InProgress.
    /// </summary>
    [HttpPut("phases/{id:guid}/validation")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> ValidatePhase(
        Guid id,
        [FromBody] ValidatePdiPhaseViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new ValidatePdiPhaseCommand(id, model.Decision, model.Reason),
            cancellationToken);
        return NoContent();
    }

    // ---- Atividades -----------------------------------------------

    /// <summary>Cria atividade na fase.</summary>
    [HttpPost("activities")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> CreateActivity(
        [FromBody] CreatePdiActivityViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new CreatePdiActivityCommand(
                model.PhaseId, model.Title, model.DueDate),
            cancellationToken)));

    /// <summary>Atualiza atividade (marcar/desmarcar concluída).</summary>
    [HttpPut("activities/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UpdateActivity(
        Guid id,
        [FromBody] UpdatePdiActivityViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdatePdiActivityCommand(
                id, model.Title, model.DueDate, model.Completed),
            cancellationToken);
        return NoContent();
    }

    /// <summary>Remove atividade.</summary>
    [HttpDelete("activities/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DeleteActivity(
        Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeletePdiActivityCommand(id), cancellationToken);
        return NoContent();
    }
}

using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Pdi;
using InfraControl.Application.Pdi;
using InfraControl.Application.Pdi.Commands;
using InfraControl.Application.Pdi.Queries;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Operations;

/// <summary>
/// PDI — Plano de Desenvolvimento Individual (docs/29 §8, decisão
/// 2026-10-03): CRUD apenas, 1 plano vigente por usuário. Operate
/// (Operator + Admin) no controller; Admin pode tudo, Operador só o
/// próprio — checagem de dono no handler (403). Falhas embrulhadas pelo
/// GlobalExceptionHandler.
/// </summary>
[Route("api/v1/pdi")]
public sealed class PdiController(ISender sender) : BaseController
{
    /// <summary>Admin: todos os planos. Operador: só o próprio (0..1 itens).</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(
            await sender.Send(new ListPdisQuery(), cancellationToken)));

    /// <summary>Cria ou atualiza o PDI do usuário (upsert).</summary>
    [HttpPut("{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Upsert(
        Guid userId,
        [FromBody] UpsertPdiViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpsertPdiCommand(
                userId, model.Strategy, model.TargetDate, model.Status,
                model.ManagerId, model.PeriodStart, model.PeriodEnd, model.Reason),
            cancellationToken)));

    /// <summary>
    /// Timeline do PDI (docs/30 §17, ADR-029) — mais recente primeiro.
    /// Admin: qualquer usuário; Operador: só o próprio (403).
    /// </summary>
    [HttpGet("{userId:guid}/history")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetHistory(
        Guid userId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetPdiHistoryQuery(userId), cancellationToken)));

    /// <summary>
    /// Avaliação final (docs/30 §16, Fase 6): plano + autoavaliação +
    /// avaliação do gestor + resultado. Leitura = Admin/dono/gestor.
    /// </summary>
    [HttpGet("{userId:guid}/evaluation")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetEvaluation(
        Guid userId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetPdiEvaluationQuery(userId), cancellationToken)));

    /// <summary>
    /// Salva a avaliação do lado do ator (§15.3): dono = autoavaliação
    /// (leva o plano a Em avaliação); gestor/Admin = avaliação do
    /// gestor (exige a autoavaliação). Upsert por lado.
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
    /// Resultado final (§16 — Fase 6, Admin/gestor): conclui o plano ou
    /// o devolve ao ciclo (Replanejado). Único por plano (2ª → 409).
    /// </summary>
    [HttpPut("{userId:guid}/result")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> FinalizeResult(
        Guid userId,
        [FromBody] FinalizePdiResultViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new FinalizePdiResultCommand(
                userId, model.Outcome, model.Notes),
            cancellationToken)));

    /// <summary>
    /// Dashboard do colaborador (docs/30 §18, Fase 7): progresso,
    /// fases, próximo prazo, pendências e totais. Leitura = Admin/dono/gestor.
    /// </summary>
    [HttpGet("{userId:guid}/dashboard")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetDashboard(
        Guid userId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetPdiDashboardQuery(userId), cancellationToken)));

    /// <summary>
    /// Dashboard do gestor (docs/30 §19, Fase 7): planos em que sou
    /// gestor, classificados atrasado/atenção/prazo + agregados.
    /// </summary>
    [HttpGet("manager/dashboard")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetManagerDashboard(
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetPdiManagerDashboardQuery(), cancellationToken)));

    /// <summary>
    /// Dashboard administrativo (docs/30 §20, Fase 7): contagens globais
    /// por status, taxa de conclusão e agregados — somente Admin.
    /// </summary>
    [HttpGet("admin/dashboard")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> GetAdminDashboard(
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetPdiAdminDashboardQuery(), cancellationToken)));
}

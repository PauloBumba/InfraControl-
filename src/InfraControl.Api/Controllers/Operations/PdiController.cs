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
            new UpsertPdiCommand(userId, model.Strategy, model.TargetDate, model.Status),
            cancellationToken)));
}

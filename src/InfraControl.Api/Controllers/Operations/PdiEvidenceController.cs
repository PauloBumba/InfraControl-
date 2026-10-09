using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Pdi;
using InfraControl.Application.Pdi.Commands;
using InfraControl.Application.Pdi.Queries;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Operations;

/// <summary>
/// Evidências do PDI (docs/30 §12, §17, Fase 4 — ADR-034): upload em
/// multipart (arquivo + tipo + descrição), revisão única do gestor
/// (validar/rejeitar com motivo) e download autenticado (nosniff +
/// disposition pelo content-type armazenado). Operate no controller;
/// permissão por ação no handler (403); erros via GlobalExceptionHandler.
/// </summary>
[Route("api/v1/pdi")]
public sealed class PdiEvidenceController(ISender sender) : BaseController
{
    /// <summary>
    /// Envia a evidência (multipart: file, phaseId, type, description).
    /// Dono do plano ou Admin; arquivo ≤ 10 MB com nome saneado.
    /// </summary>
    [HttpPost("evidences")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> CreateEvidence(
        [FromForm] IFormFile? file,
        [FromForm] Guid phaseId,
        [FromForm] string? type,
        [FromForm] string? description,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException(
                "O arquivo da evidência é obrigatório.", nameof(file));

        if (!Enum.TryParse<PdiEvidenceType>(type, ignoreCase: true, out var evidenceType))
            throw new ArgumentException(
                "O tipo de evidência é inválido.", nameof(type));

        await using var content = file.OpenReadStream();
        var dto = await sender.Send(
            new UploadPdiEvidenceCommand(
                phaseId, evidenceType, description, file.FileName,
                file.ContentType, file.Length, content),
            cancellationToken);

        return Ok(ApiResponse.Ok(dto));
    }

    /// <summary>
    /// Revisão do gestor/Admin: validada ou rejeitada (motivo
    /// obrigatório — só uma revisão por evidência).
    /// </summary>
    [HttpPut("evidences/{id:guid}/review")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> ReviewEvidence(
        Guid id,
        [FromBody] ReviewPdiEvidenceViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new ReviewPdiEvidenceCommand(id, model.Decision, model.RejectionReason),
            cancellationToken)));

    /// <summary>Download autenticado (Admin/dono/gestor) com nosniff.</summary>
    [HttpGet("evidences/{id:guid}/download")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DownloadEvidence(
        Guid id, CancellationToken cancellationToken)
    {
        var download = await sender.Send(
            new DownloadPdiEvidenceQuery(id), cancellationToken);

        Response.Headers.XContentTypeOptions = "nosniff";
        return File(download.Content, download.ContentType, download.FileName);
    }
}

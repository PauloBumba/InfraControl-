using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Api.Reporting;
using InfraControl.Application.AI.Reporting;
using InfraControl.Application.Reports.Transversal.Queries;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Reports;

/// <summary>
/// Relatório transversal (ADR-049) — visão agregada de cima dos 5 módulos
/// (produtividade/PDI, tasks, knowledge, projetos, incidentes). Read-model
/// composto, sem armazenamento novo. <c>Operate</c> (ADR-030): snapshot
/// global de operações para o Centro de Operações; dashboards por vertical
/// continuam sendo a visão pessoal/gestor.
/// </summary>
[Route("api/v1/reports")]
public sealed class ReportsController(ISender sender) : BaseController
{
    /// <summary>Snapshot agregado; filtros de período opcionais (de/até).</summary>
    [HttpGet("transversal")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetTransversal(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTransversalReportQuery(from, to), cancellationToken)));

    /// <summary>
    /// Export do relatório transversal (R-c) — PDF (padrão) ou XLSX,
    /// reaproveitando a mesma técnica dos exports do PDI (QuestPDF/ClosedXML).
    /// </summary>
    [HttpGet("transversal/export")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> ExportTransversal(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        var exportFormat = PdiExportFormat.Parse(format);
        var report = await sender.Send(
            new GetTransversalReportQuery(from, to), cancellationToken);
        var bytes = exportFormat == PdiExportFormat.Xlsx
            ? TransversalReportRenderers.RenderTransversalXlsx(report)
            : TransversalReportRenderers.RenderTransversalPdf(report);
        return File(bytes, exportFormat.ContentType,
            $"relatorio-transversal.{exportFormat.Extension}");
    }

    /// <summary>
    /// Resumo interpretativo com IA (Fase E · docs/34 §37) — LEVEL 0:
    /// lê o mesmo read-model e explica os números; sem ação executável.
    /// Sem provider configurado → 503 amigável (§44/§57).
    /// </summary>
    [HttpPost("transversal/ai-summary")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> SummarizeTransversal(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SummarizeTransversalReportCommand(from, to), cancellationToken)));
}

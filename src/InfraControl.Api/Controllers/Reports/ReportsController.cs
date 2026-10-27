using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Api.Reporting;
using InfraControl.Application.AI.Reporting;
using InfraControl.Application.Reports.Automatics.Queries;
using InfraControl.Application.Reports.Crossings.Queries;
using InfraControl.Application.Reports.Dashboards.Ports;
using InfraControl.Application.Reports.Dashboards.Queries;
using InfraControl.Application.Reports.History.Queries;
using InfraControl.Application.Reports.Indicators.Queries;
using InfraControl.Application.Reports.Modules.Queries;
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
    /// F25-T001 (docs/33 §2–§4) — indicadores detalhados de Tasks, PDI e
    /// Knowledge (volume, execução, distribuição, governança). Administer:
    /// métrica agregada de todos os módulos.
    /// </summary>
    [HttpGet("indicators")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> GetIndicators(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetReportIndicatorsQuery(from, to), cancellationToken)));

    /// <summary>
    /// F25-T002 (docs/33 §5) — cruzamentos entre módulos (Chamados→Tasks,
    /// Tasks→Knowledge, PDI→Knowledge, Knowledge→Chamados, Task→PDI).
    /// </summary>
    [HttpGet("crossings")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> GetCrossings(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetReportCrossingsQuery(from, to), cancellationToken)));

    /// <summary>
    /// F25-T003 §6 — dashboard do colaborador (minhas Tasks, meu PDI, meu
    /// conhecimento), escopado ao usuário autenticado.
    /// </summary>
    [HttpGet("dashboard/colaborador")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetColaboradorDashboard(
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetColaboradorDashboardQuery(), cancellationToken)));

    /// <summary>
    /// F25-T003 §7 — dashboard do gestor: visão operacional ampla + listas
    /// de atenção. Domínio sem subordinação, então consolidado geral.
    /// </summary>
    [HttpGet("dashboard/gestor")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetGestorDashboard(
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetGestorDashboardQuery(), cancellationToken)));

    /// <summary>
    /// F25-T003 §8 — dashboard administrativo consolidado com filtros
    /// (período, responsável, tipo). Domínio sem setor/equipe.
    /// </summary>
    [HttpGet("dashboard/administrativo")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> GetAdministrativoDashboard(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? responsibleId,
        [FromQuery] TaskKindFilter? kind,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetAdministrativoDashboardQuery(from, to, responsibleId, kind),
            cancellationToken)));

    /// <summary>F25-T004 §9 — relatório de Tasks por período.</summary>
    [HttpGet("tasks-report")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetTasksReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTasksReportQuery(from, to), cancellationToken)));

    /// <summary>F25-T004 §10 — relatório de PDI por período.</summary>
    [HttpGet("pdi-report")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetPdiReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetPdiReportQuery(from, to), cancellationToken)));

    /// <summary>F25-T004 §11 — relatório de Knowledge por período.</summary>
    [HttpGet("knowledge-report")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetKnowledgeReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetKnowledgeReportQuery(from, to), cancellationToken)));

    /// <summary>F25-T004 §12 — relatório gerencial do período.</summary>
    [HttpGet("gerencial")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetGerencialReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetGerencialReportQuery(from, to), cancellationToken)));

    /// <summary>F25-T005 §13 — série histórica mensal dos indicadores.</summary>
    [HttpGet("history")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetReportHistoryQuery(from, to), cancellationToken)));

    /// <summary>
    /// F25-T005 §14 — export do histórico (CSV padrão, PDF ou XLSX).
    /// </summary>
    [HttpGet("history/export")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> ExportHistory(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        var exportFormat = ReportExportFormat.Parse(format);
        var history = await sender.Send(
            new GetReportHistoryQuery(from, to), cancellationToken);
        var bytes = exportFormat.Value switch
        {
            "csv" => HistoryReportRenderers.RenderCsv(history),
            "xlsx" => HistoryReportRenderers.RenderXlsx(history),
            _ => HistoryReportRenderers.RenderPdf(history),
        };
        return File(bytes, exportFormat.ContentType,
            $"historico-indicadores.{exportFormat.Extension}");
    }

    /// <summary>
    /// F25-T006 §15 — relatório gerencial gerado pela cadência
    /// (mensal/trimestral/semestral/anual) sobre o período corrente.
    /// </summary>
    [HttpGet("automatic")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetAutomaticReport(
        [FromQuery] string? cadence,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetAutomaticReportQuery(cadence ?? "mensal"), cancellationToken)));

    /// <summary>
    /// F25-T006 §16 — interpretação por IA do relatório gerencial (LEVEL 0).
    /// Sem provider configurado → 503 amigável.
    /// </summary>
    [HttpPost("gerencial/ai-interpretation")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> SummarizeGerencial(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SummarizeGerencialReportCommand(from, to), cancellationToken)));

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

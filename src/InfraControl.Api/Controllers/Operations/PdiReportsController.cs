using System.Globalization;
using InfraControl.Api.Authorization;
using InfraControl.Api.Reporting;
using InfraControl.Application.Pdi.Queries;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Operations;

/// <summary>
/// PDI — indicadores e relatórios (docs/30 §21, §22 — Fase 8, ADR-038).
/// Leitura apenas (sem mutação): indicadores, relatório semestral e de
/// dificuldades = Administer; relatório individual = Admin/dono/gestor
/// (guard no handler, mesmo leitor do evaluation). Export
/// <c>?format=pdf|xlsx</c> reusa o mesmo DTO; formato/data inválidos =
/// 400 (ArgumentException no GlobalExceptionHandler).
/// </summary>
[Route("api/v1/pdi")]
public sealed class PdiReportsController(ISender sender) : BaseController
{
    /// <summary>§21 — indicadores consolidados (filtro de período opcional).</summary>
    [HttpGet("indicators")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> GetIndicators(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetPdiIndicatorsQuery(
                ParseDate(from, nameof(from)), ParseDate(to, nameof(to))),
            cancellationToken)));

    /// <summary>§22 — relatório individual (estrutura + acompanhamento + avaliação).</summary>
    [HttpGet("reports/individual/{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GetIndividualReport(
        Guid userId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetPdiIndividualReportQuery(userId), cancellationToken)));

    /// <summary>§22 — export do relatório individual (PDF padrão).</summary>
    [HttpGet("reports/individual/{userId:guid}/export")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> ExportIndividualReport(
        Guid userId,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        var exportFormat = PdiExportFormat.Parse(format);
        var report = await sender.Send(
            new GetPdiIndividualReportQuery(userId), cancellationToken);
        var bytes = exportFormat == PdiExportFormat.Xlsx
            ? PdiReportRenderers.RenderIndividualXlsx(report)
            : PdiReportRenderers.RenderIndividualPdf(report);
        return File(bytes, exportFormat.ContentType,
            FileName("relatorio-individual", exportFormat));
    }

    /// <summary>§22 — relatório semestral (padrão = semestre corrente).</summary>
    [HttpGet("reports/semester")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> GetSemesterReport(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetPdiSemesterReportQuery(
                ParseDate(from, nameof(from)), ParseDate(to, nameof(to))),
            cancellationToken)));

    /// <summary>§22 — export do relatório semestral (PDF padrão).</summary>
    [HttpGet("reports/semester/export")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> ExportSemesterReport(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        var exportFormat = PdiExportFormat.Parse(format);
        var report = await sender.Send(
            new GetPdiSemesterReportQuery(
                ParseDate(from, nameof(from)), ParseDate(to, nameof(to))),
            cancellationToken);
        var bytes = exportFormat == PdiExportFormat.Xlsx
            ? PdiReportRenderers.RenderSemesterXlsx(report)
            : PdiReportRenderers.RenderSemesterPdf(report);
        return File(bytes, exportFormat.ContentType,
            FileName("relatorio-semestral", exportFormat));
    }

    /// <summary>§22 — relatório de dificuldades (distribuição por tipo no período).</summary>
    [HttpGet("reports/difficulties")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> GetDifficultiesReport(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetPdiDifficultiesReportQuery(
                ParseDate(from, nameof(from)), ParseDate(to, nameof(to))),
            cancellationToken)));

    /// <summary>§22 — export do relatório de dificuldades (PDF padrão).</summary>
    [HttpGet("reports/difficulties/export")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> ExportDifficultiesReport(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        var exportFormat = PdiExportFormat.Parse(format);
        var report = await sender.Send(
            new GetPdiDifficultiesReportQuery(
                ParseDate(from, nameof(from)), ParseDate(to, nameof(to))),
            cancellationToken);
        var bytes = exportFormat == PdiExportFormat.Xlsx
            ? PdiReportRenderers.RenderDifficultiesXlsx(report)
            : PdiReportRenderers.RenderDifficultiesPdf(report);
        return File(bytes, exportFormat.ContentType,
            FileName("relatorio-dificuldades", exportFormat));
    }

    /// <summary>AAAA-MM-DD (invariante); nulo/vazio = sem filtro; inválido = 400.</summary>
    internal static DateOnly? ParseDate(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateOnly.TryParse(
                value, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var parsed))
            return parsed;

        throw new ArgumentException(
            $"Data '{name}' inválida — use o formato AAAA-MM-DD.");
    }

    private static string FileName(string slug, PdiExportFormat format)
        => $"pdi-{slug}-{DateTime.UtcNow:yyyyMMdd}.{format.Extension}";
}

using System.Security.Claims;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Company;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Hosting;

namespace InfraControl.Api.Reporting;

/// <summary>
/// F52-T004 — monta o <see cref="ReportBranding"/> de um export
/// (empresa via <see cref="GetCompanySettingsQuery"/>, logo lido do disco,
/// autoria = claim <c>displayName</c> do operador ou e-mail) e grava o
/// <see cref="AuditEntry"/> de exportação (resource "ReportExport").
/// </summary>
public static class ReportBrandingFactory
{
    /// <summary>Branding pronto para os renderers.</summary>
    public static async Task<ReportBranding> BuildAsync(
        ISender sender,
        IWebHostEnvironment environment,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var settings = await sender.Send(
            new GetCompanySettingsQuery(), cancellationToken);
        var logo = ReportBrandingLogo.Load(environment, settings?.LogoPath);
        return ReportBranding.From(settings, GeneratedBy(user), logo);
    }

    /// <summary>
    /// Auditoria da exportação: action
    /// <c>ReportExport.{slug}.{format}</c>, ator = e-mail do operador
    /// (mesmo formato que o <c>AuditReader</c> casa com o usuário).
    /// </summary>
    public static Task AuditExportAsync(
        IAuditWriter auditWriter,
        string reportSlug,
        string format,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
        => auditWriter.WriteAsync(
            new AuditEntry(
                ReportBranding.AuditAction(reportSlug, format),
                "ReportExport",
                "Succeeded",
                user.FindFirstValue(ClaimTypes.Email)
                    ?? user.Identity?.Name),
            cancellationToken);

    /// <summary>Nome de exibição do operador (displayName → e-mail).</summary>
    public static string? GeneratedBy(ClaimsPrincipal user)
        => user.FindFirstValue("displayName")
            ?? user.FindFirstValue(ClaimTypes.Email)
            ?? user.Identity?.Name;
}

using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Company.Ports;
using InfraControl.Application.Discovery.Runs.Ports;
using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Reports.Scheduled;

/// <summary>
/// Conteúdo do relatório geral — somente dados reais agregados dos
/// repositórios (nunca inventado). Janela "últimas 24h" para discovery.
/// </summary>
public sealed record GeneralReportContent(
    DateTime GeneratedAtUtc,
    int TotalDevices,
    IReadOnlyDictionary<string, int> DevicesByType,
    int OpenIncidents,
    int OpenAlerts,
    int DiscoveryRunsLast24h,
    int DiscoveryRunsFailedLast24h,
    int DiscoveryRunsCompletedLast24h,
    /// <summary>Nome exibido no cabeçalho/rodapé (empresa ou "InfraControl").</summary>
    string CompanyName,
    /// <summary>Linha de dados institucionais (CNPJ, endereço, fone, e-mail).</summary>
    string? CompanyDetails);

/// <summary>
/// Monta o relatório geral + assunto/corpo do e-mail. Registrado como
/// scoped; chamado pelo ScheduledReportWorker (sem sessão HTTP — leitura
/// pura, não passa pelo pipeline MediatR).
/// </summary>
public sealed class GeneralReportBuilder(
    IDeviceRepository devices,
    IIncidentRepository incidents,
    IAlertRepository alerts,
    IDiscoveryRunRepository discoveryRuns,
    ICompanySettingsRepository companySettings)
{
    private static readonly IncidentStatus[] OpenIncidentStatuses =
    [
        IncidentStatus.Open,
        IncidentStatus.Investigating,
        IncidentStatus.Identified,
        IncidentStatus.Monitoring,
    ];

    public async Task<GeneralReportContent> BuildAsync(CancellationToken cancellationToken)
    {
        var allDevices = await devices.GetAllOrderedByNameAsync(cancellationToken);
        var devicesByType = allDevices
            .GroupBy(d => d.Type.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var openIncidents = await incidents.SearchAsync(
            null, OpenIncidentStatuses, 1, 1, cancellationToken);

        var openAlerts = await alerts.SearchAsync(
            null, AlertStatus.Open, 1, 1, cancellationToken);

        var since = DateTime.UtcNow.AddHours(-24);
        // TotalCount é exato independente do pageSize; os itens da página
        // (máx. 200) bastam para o breakdown completed/failed do dia.
        var allRuns = await discoveryRuns.SearchAsync(
            null, 1, 200, cancellationToken, startedAfterUtc: since);
        var failed = allRuns.Items.Count(r => r.Status == DiscoveryRunStatus.Failed);
        var completed = allRuns.Items.Count(r => r.Status == DiscoveryRunStatus.Completed);

        var company = await companySettings.GetAsync(cancellationToken);
        var companyName = string.IsNullOrWhiteSpace(company?.Name)
            ? "InfraControl" : company!.Name;
        var companyDetails = BuildCompanyDetails(company);

        return new GeneralReportContent(
            DateTime.UtcNow,
            allDevices.Count,
            devicesByType,
            openIncidents.TotalCount,
            openAlerts.TotalCount,
            allRuns.TotalCount,
            failed,
            completed,
            companyName,
            companyDetails);
    }

    /// <summary>Linha única com os dados institucionais cadastrados (CNPJ, endereço, fone, e-mail, site).</summary>
    private static string? BuildCompanyDetails(
        Domain.Entities.CompanySettings? company)
    {
        if (company is null) return null;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(company.LegalName)) parts.Add(company.LegalName);
        if (!string.IsNullOrWhiteSpace(company.Cnpj)) parts.Add($"CNPJ {company.Cnpj}");
        var address = string.Join(", ", new[]
            {
                company.Street, company.Number, company.Complement,
                company.District, company.City, company.State, company.ZipCode
            }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        if (!string.IsNullOrWhiteSpace(address)) parts.Add(address);
        if (!string.IsNullOrWhiteSpace(company.Phone)) parts.Add(company.Phone);
        if (!string.IsNullOrWhiteSpace(company.Email)) parts.Add(company.Email);
        if (!string.IsNullOrWhiteSpace(company.Website)) parts.Add(company.Website);
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static readonly TimeZoneInfo SaoPauloTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private static DateTime ToSaoPaulo(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc), SaoPauloTimeZone);

    public static (string Subject, string HtmlBody, string TextBody) ComposeEmail(
        GeneralReportContent report)
    {
        var generatedAtBr = ToSaoPaulo(report.GeneratedAtUtc);
        var subject =
            $"{report.CompanyName} — Relatório geral {generatedAtBr:dd/MM/yyyy HH:mm}";

        var headerLines = new List<string> { report.CompanyName };
        if (!string.IsNullOrWhiteSpace(report.CompanyDetails))
            headerLines.Add(report.CompanyDetails!);
        var headerText = string.Join('\n', headerLines);

        var footerText =
            $"{report.CompanyName}" +
            (!string.IsNullOrWhiteSpace(report.CompanyDetails)
                ? $" — {report.CompanyDetails}" : "") +
            $"\nRelatório automático · gerado em {generatedAtBr:dd/MM/yyyy HH:mm} (horário de Brasília)";

        var typeLines = report.DevicesByType
            .OrderByDescending(kv => kv.Value)
            .Select(kv => $"  • {kv.Key}: {kv.Value}")
            .ToArray();

        var lines = new List<string>
        {
            headerText,
            $"Relatório geral da infraestrutura — {generatedAtBr:dd/MM/yyyy HH:mm} (horário de Brasília)",
            "",
            "INVENTÁRIO",
            $"  • Devices totais: {report.TotalDevices}",
        };
        lines.AddRange(typeLines.Select(l => l.TrimStart()));
        lines.AddRange(
        [
            "",
            "OPERAÇÃO",
            $"  • Incidentes não resolvidos: {report.OpenIncidents}",
            $"  • Alertas abertos: {report.OpenAlerts}",
            $"  • Discovery runs (24h): {report.DiscoveryRunsLast24h} " +
                $"({report.DiscoveryRunsCompletedLast24h} ok, {report.DiscoveryRunsFailedLast24h} falhas)",
            "",
            footerText,
        ]);
        var text = string.Join('\n', lines);

        var typeRows = report.DevicesByType
            .OrderByDescending(kv => kv.Value)
            .Select(kv =>
                $"<tr><td style='padding:4px 12px'>{kv.Key}</td>" +
                $"<td style='padding:4px 12px;text-align:right'>{kv.Value}</td></tr>")
            .Aggregate("", (acc, row) => acc + row);

        var headerHtml =
            "<div style='border-bottom:2px solid #0f172a;padding-bottom:8px;margin-bottom:16px'>" +
            $"<h2 style='margin:0;font-size:18px'>{report.CompanyName}</h2>" +
            (!string.IsNullOrWhiteSpace(report.CompanyDetails)
                ? $"<p style='margin:2px 0 0;color:#64748b;font-size:12px'>{report.CompanyDetails}</p>"
                : "") +
            "</div>";

        var html =
            "<div style='font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1e293b'>" +
            headerHtml +
            "<h3 style='margin:0 0 8px'>Relatório geral — Infraestrutura</h3>" +
            $"<p style='margin:0 0 16px;color:#64748b'>Gerado em " +
            $"{generatedAtBr:dd/MM/yyyy HH:mm} (horário de Brasília)</p>" +
            "<h3 style='margin:16px 0 8px'>Inventário</h3>" +
            "<table style='border-collapse:collapse'>" +
            $"<tr><td style='padding:4px 12px'><b>Devices totais</b></td>" +
            $"<td style='padding:4px 12px;text-align:right'><b>{report.TotalDevices}</b></td></tr>" +
            typeRows +
            "</table>" +
            "<h3 style='margin:16px 0 8px'>Operação</h3>" +
            "<table style='border-collapse:collapse'>" +
            $"<tr><td style='padding:4px 12px'>Incidentes não resolvidos</td>" +
            $"<td style='padding:4px 12px;text-align:right'>{report.OpenIncidents}</td></tr>" +
            $"<tr><td style='padding:4px 12px'>Alertas abertos</td>" +
            $"<td style='padding:4px 12px;text-align:right'>{report.OpenAlerts}</td></tr>" +
            $"<tr><td style='padding:4px 12px'>Discovery runs (24h)</td>" +
            $"<td style='padding:4px 12px;text-align:right'>{report.DiscoveryRunsLast24h} " +
            $"({report.DiscoveryRunsCompletedLast24h} ok, {report.DiscoveryRunsFailedLast24h} falhas)</td></tr>" +
            "</table>" +
            "<p style='margin-top:24px;padding-top:8px;border-top:1px solid #e2e8f0;color:#94a3b8;font-size:12px'>" +
            $"{report.CompanyName}" +
            (!string.IsNullOrWhiteSpace(report.CompanyDetails)
                ? $" — {report.CompanyDetails}" : "") +
            $"<br>Relatório automático · gerado em {generatedAtBr:dd/MM/yyyy HH:mm} (horário de Brasília).</p>" +
            "</div>";

        return (subject, html, text);
    }
}

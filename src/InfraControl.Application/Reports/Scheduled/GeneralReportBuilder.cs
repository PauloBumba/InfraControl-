using InfraControl.Application.Alerts.Ports;
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
    int DiscoveryRunsCompletedLast24h);

/// <summary>
/// Monta o relatório geral + assunto/corpo do e-mail. Registrado como
/// scoped; chamado pelo ScheduledReportWorker (sem sessão HTTP — leitura
/// pura, não passa pelo pipeline MediatR).
/// </summary>
public sealed class GeneralReportBuilder(
    IDeviceRepository devices,
    IIncidentRepository incidents,
    IAlertRepository alerts,
    IDiscoveryRunRepository discoveryRuns)
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

        return new GeneralReportContent(
            DateTime.UtcNow,
            allDevices.Count,
            devicesByType,
            openIncidents.TotalCount,
            openAlerts.TotalCount,
            allRuns.TotalCount,
            failed,
            completed);
    }

    public static (string Subject, string HtmlBody, string TextBody) ComposeEmail(
        GeneralReportContent report)
    {
        var subject =
            $"InfraControl — Relatório geral {report.GeneratedAtUtc:dd/MM/yyyy HH:mm} UTC";

        var typeLines = report.DevicesByType
            .OrderByDescending(kv => kv.Value)
            .Select(kv => $"  • {kv.Key}: {kv.Value}")
            .ToArray();

        var lines = new List<string>
        {
            "Relatório geral da infraestrutura — InfraControl",
            $"Gerado em: {report.GeneratedAtUtc:dd/MM/yyyy HH:mm} UTC",
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
            "Este é um relatório automático gerado pela agenda configurada " +
            "em Relatórios → Relatório agendado.",
        ]);
        var text = string.Join('\n', lines);

        var typeRows = report.DevicesByType
            .OrderByDescending(kv => kv.Value)
            .Select(kv =>
                $"<tr><td style='padding:4px 12px'>{kv.Key}</td>" +
                $"<td style='padding:4px 12px;text-align:right'>{kv.Value}</td></tr>")
            .Aggregate("", (acc, row) => acc + row);

        var html =
            "<div style='font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1e293b'>" +
            "<h2 style='margin:0 0 8px'>Relatório geral — InfraControl</h2>" +
            $"<p style='margin:0 0 16px;color:#64748b'>Gerado em " +
            $"{report.GeneratedAtUtc:dd/MM/yyyy HH:mm} UTC</p>" +
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
            "<p style='margin-top:24px;color:#94a3b8;font-size:12px'>" +
            "Relatório automático da agenda em Relatórios → Relatório agendado.</p>" +
            "</div>";

        return (subject, html, text);
    }
}

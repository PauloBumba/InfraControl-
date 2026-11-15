using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Company.Ports;
using InfraControl.Application.Discovery.Runs.Ports;
using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Notifications.Stats.Ports;
using InfraControl.Application.Pdi.Ports;
using InfraControl.Application.Projects.Ports;
using InfraControl.Application.Sla.TaskSlas.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Reports.Scheduled;

/// <summary>Entregas de notificação de um canal nas últimas 24h.</summary>
public sealed record DeliveryChannelStat(int Sent, int Failed, int Pending);

/// <summary>
/// Conteúdo do relatório geral — somente dados reais agregados dos
/// repositórios (nunca inventado). Janela "últimas 24h" para discovery
/// e entregas de notificação.
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
    string? CompanyDetails,
    /// <summary>Alertas Open por severidade (todas as origens).</summary>
    IReadOnlyDictionary<string, int> OpenAlertsBySeverity,
    /// <summary>Incidentes por status (abertos detalhados + resolvidos).</summary>
    IReadOnlyDictionary<string, int> IncidentsByStatus,
    /// <summary>Tasks por estado.</summary>
    IReadOnlyDictionary<string, int> TasksByState,
    /// <summary>Tasks abertas com SLA violado.</summary>
    int SlaViolatedOpenTasks,
    /// <summary>Projetos por status.</summary>
    IReadOnlyDictionary<string, int> ProjectsByStatus,
    /// <summary>PDIs por status.</summary>
    IReadOnlyDictionary<string, int> PdisByStatus,
    /// <summary>Entregas de notificação (24h) por canal.</summary>
    IReadOnlyDictionary<string, DeliveryChannelStat> NotificationDeliveriesLast24h);

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
    ICompanySettingsRepository companySettings,
    ITaskItemRepository tasks,
    ITaskSlaRepository taskSlas,
    INotificationDeliveryStatsStore deliveryStats,
    IProjectRepository projects,
    IPdiRepository pdis)
{
    private static readonly IncidentStatus[] OpenIncidentStatuses =
    [
        IncidentStatus.Open,
        IncidentStatus.Investigating,
        IncidentStatus.Identified,
        IncidentStatus.Monitoring,
    ];

    private static readonly IncidentStatus[] AllIncidentStatuses =
        [.. OpenIncidentStatuses, IncidentStatus.Resolved];

    private static readonly TaskState[] AllTaskStates =
        Enum.GetValues<TaskState>();

    public async Task<GeneralReportContent> BuildAsync(CancellationToken cancellationToken)
    {
        var allDevices = await devices.GetAllOrderedByNameAsync(cancellationToken);
        var devicesByType = allDevices
            .GroupBy(d => d.Type.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var since = DateTime.UtcNow.AddHours(-24);

        // Incidentes por status — pageSize=1, só o TotalCount exato interessa.
        var incidentsByStatus = new Dictionary<string, int>();
        var openIncidents = 0;
        foreach (var status in AllIncidentStatuses)
        {
            var count = await incidents.SearchAsync(
                null, [status], 1, 1, cancellationToken);
            incidentsByStatus[status.ToString()] = count.TotalCount;
            if (status != IncidentStatus.Resolved)
                openIncidents += count.TotalCount;
        }

        // Alertas: total Open + breakdown por severidade (todas as origens).
        var openAlerts = await alerts.SearchAsync(
            null, AlertStatus.Open, 1, 1, cancellationToken);
        var alertsBySeverity = (await alerts.GetOpenCountsBySeverityAsync(cancellationToken))
            .ToDictionary(row => row.Severity.ToString(), row => row.Count);

        // Tasks por estado + SLA violado em tasks abertas.
        var tasksByState = new Dictionary<string, int>();
        foreach (var state in AllTaskStates)
        {
            var count = await tasks.SearchAsync(
                null, null, state, 1, 1, cancellationToken: cancellationToken);
            if (count.TotalCount > 0)
                tasksByState[state.ToString()] = count.TotalCount;
        }
        var slaViolated = await taskSlas.CountOpenBreachedAsync(cancellationToken);

        // Projetos e PDIs por status (coleções pequenas — group em memória).
        var allProjects = await projects.ListAllAsync(cancellationToken);
        var projectsByStatus = allProjects
            .GroupBy(p => p.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var allPdis = await pdis.ListWithOwnersAsync(
            cancellationToken: cancellationToken);
        var pdisByStatus = allPdis
            .GroupBy(p => p.Plan.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        // Entregas de notificação nas últimas 24h por canal × status.
        var deliveryRows = await deliveryStats.ListSinceAsync(since, cancellationToken);
        var deliveriesByChannel = deliveryRows
            .GroupBy(row => row.Channel.ToString())
            .ToDictionary(
                g => g.Key,
                g => new DeliveryChannelStat(
                    g.Count(r => r.Status == DeliveryStatus.Sent),
                    g.Count(r => r.Status == DeliveryStatus.Failed),
                    g.Count(r => r.Status is DeliveryStatus.Pending
                        or DeliveryStatus.Processing)));
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
            openIncidents,
            openAlerts.TotalCount,
            allRuns.TotalCount,
            failed,
            completed,
            companyName,
            companyDetails,
            alertsBySeverity,
            incidentsByStatus,
            tasksByState,
            slaViolated,
            projectsByStatus,
            pdisByStatus,
            deliveriesByChannel);
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
        lines.Add("");
        lines.Add("MONITORAMENTO");
        lines.Add($"  • Alertas abertos: {report.OpenAlerts}");
        foreach (var kv in report.OpenAlertsBySeverity.OrderByDescending(kv => kv.Value))
            lines.Add($"      - {kv.Key}: {kv.Value}");
        lines.Add($"  • Incidentes não resolvidos: {report.OpenIncidents}");
        foreach (var kv in report.IncidentsByStatus.OrderBy(kv => kv.Key))
            lines.Add($"      - {kv.Key}: {kv.Value}");
        lines.Add(
            $"  • Discovery runs (24h): {report.DiscoveryRunsLast24h} " +
            $"({report.DiscoveryRunsCompletedLast24h} ok, {report.DiscoveryRunsFailedLast24h} falhas)");
        lines.Add("");
        lines.Add("TAREFAS E PROJETOS");
        if (report.TasksByState.Count == 0)
            lines.Add("  • Tasks: nenhuma");
        foreach (var kv in report.TasksByState.OrderByDescending(kv => kv.Value))
            lines.Add($"  • Tasks {kv.Key}: {kv.Value}");
        lines.Add($"  • Tasks com SLA violado (abertas): {report.SlaViolatedOpenTasks}");
        if (report.ProjectsByStatus.Count == 0)
            lines.Add("  • Projetos: nenhum");
        foreach (var kv in report.ProjectsByStatus.OrderByDescending(kv => kv.Value))
            lines.Add($"  • Projetos {kv.Key}: {kv.Value}");
        if (report.PdisByStatus.Count == 0)
            lines.Add("  • PDIs: nenhum");
        foreach (var kv in report.PdisByStatus.OrderByDescending(kv => kv.Value))
            lines.Add($"  • PDIs {kv.Key}: {kv.Value}");
        lines.Add("");
        lines.Add("NOTIFICAÇÕES (últimas 24h)");
        if (report.NotificationDeliveriesLast24h.Count == 0)
            lines.Add("  • Nenhuma entrega no período");
        foreach (var kv in report.NotificationDeliveriesLast24h.OrderByDescending(kv => kv.Value.Sent + kv.Value.Failed))
            lines.Add(
                $"  • {kv.Key}: {kv.Value.Sent} enviadas, " +
                $"{kv.Value.Failed} falhas, {kv.Value.Pending} pendentes");
        lines.Add("");
        lines.Add(footerText);
        var text = string.Join('\n', lines);

        var typeRows = report.DevicesByType
            .OrderByDescending(kv => kv.Value)
            .Select(kv =>
                $"<tr><td style='padding:4px 12px'>{kv.Key}</td>" +
                $"<td style='padding:4px 12px;text-align:right'>{kv.Value}</td></tr>")
            .Aggregate("", (acc, row) => acc + row);

        static string DictRows(
            IOrderedEnumerable<KeyValuePair<string, int>> ordered)
            => ordered
                .Select(kv =>
                    $"<tr><td style='padding:4px 12px'>{kv.Key}</td>" +
                    $"<td style='padding:4px 12px;text-align:right'>{kv.Value}</td></tr>")
                .Aggregate("", (acc, row) => acc + row);

        var severityRows = DictRows(
            report.OpenAlertsBySeverity.OrderByDescending(kv => kv.Value));
        var incidentRows = DictRows(
            report.IncidentsByStatus.OrderBy(kv => kv.Key));
        var taskRows = DictRows(
            report.TasksByState.OrderByDescending(kv => kv.Value));
        var projectRows = DictRows(
            report.ProjectsByStatus.OrderByDescending(kv => kv.Value));
        var pdiRows = DictRows(
            report.PdisByStatus.OrderByDescending(kv => kv.Value));

        var deliveryRowsHtml = report.NotificationDeliveriesLast24h.Count == 0
            ? "<tr><td style='padding:4px 12px' colspan='4'>Nenhuma entrega no período</td></tr>"
            : report.NotificationDeliveriesLast24h
                .OrderByDescending(kv => kv.Value.Sent + kv.Value.Failed)
                .Select(kv =>
                    $"<tr><td style='padding:4px 12px'>{kv.Key}</td>" +
                    $"<td style='padding:4px 12px;text-align:right'>{kv.Value.Sent}</td>" +
                    $"<td style='padding:4px 12px;text-align:right'>{kv.Value.Failed}</td>" +
                    $"<td style='padding:4px 12px;text-align:right'>{kv.Value.Pending}</td></tr>")
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
            "<h3 style='margin:16px 0 8px'>Monitoramento</h3>" +
            "<table style='border-collapse:collapse'>" +
            $"<tr><td style='padding:4px 12px'><b>Alertas abertos</b></td>" +
            $"<td style='padding:4px 12px;text-align:right'><b>{report.OpenAlerts}</b></td></tr>" +
            severityRows +
            $"<tr><td style='padding:4px 12px'><b>Incidentes não resolvidos</b></td>" +
            $"<td style='padding:4px 12px;text-align:right'><b>{report.OpenIncidents}</b></td></tr>" +
            incidentRows +
            $"<tr><td style='padding:4px 12px'>Discovery runs (24h)</td>" +
            $"<td style='padding:4px 12px;text-align:right'>{report.DiscoveryRunsLast24h} " +
            $"({report.DiscoveryRunsCompletedLast24h} ok, {report.DiscoveryRunsFailedLast24h} falhas)</td></tr>" +
            "</table>" +
            "<h3 style='margin:16px 0 8px'>Tarefas e Projetos</h3>" +
            "<table style='border-collapse:collapse'>" +
            taskRows +
            $"<tr><td style='padding:4px 12px'><b>Tasks com SLA violado (abertas)</b></td>" +
            $"<td style='padding:4px 12px;text-align:right'><b>{report.SlaViolatedOpenTasks}</b></td></tr>" +
            projectRows +
            pdiRows +
            "</table>" +
            "<h3 style='margin:16px 0 8px'>Notificações (últimas 24h)</h3>" +
            "<table style='border-collapse:collapse'>" +
            "<tr><th style='padding:4px 12px;text-align:left'>Canal</th>" +
            "<th style='padding:4px 12px;text-align:right'>Enviadas</th>" +
            "<th style='padding:4px 12px;text-align:right'>Falhas</th>" +
            "<th style='padding:4px 12px;text-align:right'>Pendentes</th></tr>" +
            deliveryRowsHtml +
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

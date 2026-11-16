using ClosedXML.Excel;
using InfraControl.Application.Reports.Modules;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InfraControl.Api.Reporting;

/// <summary>
/// Renderização do relatório de evolução por usuário (PDF QuestPDF +
/// XLSX ClosedXML), mesmo padrão dos renderers do transversal/PDI —
/// header/footer com <see cref="ReportBranding"/> (dados da empresa).
/// </summary>
public static class UserEvolutionReportRenderers
{
    static UserEvolutionReportRenderers()
        => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] RenderPdf(
        UserEvolutionReportDto report, ReportBranding? branding = null)
    {
        var b = branding ?? ReportBranding.Fallback();
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(text => text.FontSize(9));
                page.Header().Column(column =>
                {
                    ReportBrandingHeader.Render(column, b);
                    column.Item().PaddingTop(6).Text(text =>
                        text.Span("Evolução por usuário").FontSize(15).Bold());
                    column.Item().Text(text => text.Span(Period(report.From, report.To))
                        .FontSize(9).FontColor(Colors.Grey.Darken1));
                });
                page.Content().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2.2f);   // Usuário
                        columns.RelativeColumn(1.1f);   // Tasks
                        columns.RelativeColumn(1.1f);   // Projetos
                        columns.RelativeColumn(1.4f);   // PDI
                        columns.RelativeColumn(1f);     // Conhecimento
                    });
                    table.Header(header =>
                    {
                        foreach (var title in new[]
                                 { "Usuário", "Tasks", "Projetos", "PDI", "Conhecimento" })
                            header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                .Padding(4).Text(title).Bold().FontSize(8);
                    });
                    foreach (var row in report.Users)
                    {
                        var tasks = $"{row.TasksCompleted}/{row.TasksCreated} concl." +
                                    (row.TasksOverdue > 0 ? $" · {row.TasksOverdue} atr." : "") +
                                    (row.TasksInProgress > 0 ? $" · {row.TasksInProgress} and." : "");
                        var projects = $"{row.ProjectsCompleted} concl. · " +
                                       $"{row.ProjectsLed} líd. · {row.ProjectsMember} memb.";
                        var pdi = row.PdiStatus is null
                            ? "—"
                            : $"{PdiLabel(row.PdiStatus)}" +
                              (row.PdiProgressPercent is { } pct ? $" · {pct}%" : "") +
                              (row.PdiTargetDate is { } d ? $" · {d:dd/MM/yyyy}" : "");
                        var knowledge = row.KnowledgeArticles.ToString();

                        foreach (var cell in new[] { row.Name, tasks, projects, pdi, knowledge })
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(4).Text(cell).FontSize(8);
                    }
                });
                page.Footer().Column(footer =>
                {
                    ReportBrandingFooter.Render(footer, b, "Relatórios");
                });
            });
        }).GeneratePdf();
    }

    public static byte[] RenderXlsx(
        UserEvolutionReportDto report, ReportBranding? branding = null)
    {
        var b = branding ?? ReportBranding.Fallback();
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Evolução");

        var nextRow = ReportBrandingXlsx.RenderHeader(sheet, b);
        sheet.Cell(nextRow, 1).Value = "Evolução por usuário";
        sheet.Cell(nextRow + 1, 1).Value = Period(report.From, report.To);
        var headerRow = nextRow + 3;
        var headers = new[]
        {
            "Usuário", "E-mail",
            "Tasks criadas", "Tasks concluídas", "Tasks atrasadas", "Tasks em andamento",
            "Projetos liderados", "Projetos membro", "Projetos concluídos",
            "PDI status", "PDI progresso %", "PDI prazo", "Artigos knowledge",
        };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(headerRow, i + 1).Value = headers[i];
            sheet.Cell(headerRow, i + 1).Style.Font.Bold = true;
        }

        var row = headerRow + 1;
        foreach (var u in report.Users)
        {
            sheet.Cell(row, 1).Value = u.Name;
            sheet.Cell(row, 2).Value = u.Email;
            sheet.Cell(row, 3).Value = u.TasksCreated;
            sheet.Cell(row, 4).Value = u.TasksCompleted;
            sheet.Cell(row, 5).Value = u.TasksOverdue;
            sheet.Cell(row, 6).Value = u.TasksInProgress;
            sheet.Cell(row, 7).Value = u.ProjectsLed;
            sheet.Cell(row, 8).Value = u.ProjectsMember;
            sheet.Cell(row, 9).Value = u.ProjectsCompleted;
            sheet.Cell(row, 10).Value = u.PdiStatus is null ? "—" : PdiLabel(u.PdiStatus);
            sheet.Cell(row, 11).Value = u.PdiProgressPercent;
            sheet.Cell(row, 12).Value = u.PdiTargetDate?.ToString("dd/MM/yyyy");
            sheet.Cell(row, 13).Value = u.KnowledgeArticles;
            row++;
        }

        ReportBrandingXlsx.RenderFooter(sheet, b, row + 1);
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string PdiLabel(string status) => status switch
    {
        "Draft" => "Rascunho",
        "Planned" => "Planejado",
        "AwaitingAcceptance" => "Aguardando aceite",
        "Active" => "Ativo",
        "InProgress" => "Em andamento",
        "UnderEvaluation" => "Em avaliação",
        "Completed" => "Concluído",
        "Replanned" => "Replanejado",
        "Cancelled" => "Cancelado",
        _ => status,
    };

    private static string Period(DateOnly? from, DateOnly? to)
        => $"{from?.ToString("dd/MM/yyyy") ?? "início"} – " +
           $"{to?.ToString("dd/MM/yyyy") ?? "hoje"}";
}

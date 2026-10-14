using ClosedXML.Excel;
using InfraControl.Application.Reports.Transversal;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InfraControl.Api.Reporting;

/// <summary>
/// Renderização do relatório transversal (ADR-049): PDF (QuestPDF,
/// licença Community) e XLSX (ClosedXML), no mesmo técnica/estilo dos
/// renderers do PDI (docs/30 §22) mas auto-contido (sem acoplar ao módulo
/// PDI). Recebe o DTO pronto do read-model e devolve os bytes.
/// </summary>
public static class TransversalReportRenderers
{
    static TransversalReportRenderers()
        => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] RenderTransversalPdf(TransversalReportDto report)
        => Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(text => text.FontSize(10));
                page.Header().Column(column =>
                {
                    column.Item().Text(text =>
                        text.Span("Relatório Transversal").FontSize(16).Bold());
                    column.Item().Text(text => text.Span(Period(report.From, report.To))
                        .FontSize(9).FontColor(Colors.Grey.Darken1));
                });
                page.Content().PaddingTop(16).Column(column =>
                {
                    Block(column.Item(), "Produtividade (PDI)", [
                        ("Planos", report.Pdi.Total.ToString()),
                        ("Concluídos", report.Pdi.Completed.ToString()),
                        ("Por status", JoinCounts(report.Pdi.ByStatus)),
                    ]);
                    Block(column.Item(), "Tarefas", [
                        ("Total", report.Tasks.Total.ToString()),
                        ("Concluídas", report.Tasks.Completed.ToString()),
                        ("Atrasadas", report.Tasks.Overdue.ToString()),
                        ("Por estado", JoinCounts(report.Tasks.ByState)),
                    ]);
                    Block(column.Item(), "Conhecimento", [
                        ("Artigos", report.Knowledge.Total.ToString()),
                        ("Publicados", report.Knowledge.Published.ToString()),
                        ("Rascunhos", report.Knowledge.Draft.ToString()),
                    ]);
                    Block(column.Item(), "Projetos", [
                        ("Total", report.Projects.Total.ToString()),
                        ("Por status", JoinCounts(report.Projects.ByStatus)),
                    ]);
                    Block(column.Item(), "Incidentes", [
                        ("Total", report.Incidents.Total.ToString()),
                        ("Resolvidos", report.Incidents.Resolved.ToString()),
                        ("Tempo médio de resolução",
                            report.Incidents.AverageResolutionHours is { } hours
                                ? $"{hours:F1} h"
                                : "—"),
                    ]);
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("InfraControl · Relatórios · página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

    public static byte[] RenderTransversalXlsx(TransversalReportDto report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Transversal");

        sheet.Cell(1, 1).Value = "Relatório Transversal";
        sheet.Cell(2, 1).Value = Period(report.From, report.To);
        sheet.Cell(4, 1).Value = "Módulo";
        sheet.Cell(4, 2).Value = "Métrica";
        sheet.Cell(4, 3).Value = "Valor";
        foreach (var column in new[] { 1, 2, 3 })
            sheet.Cell(4, column).Style.Font.Bold = true;

        var rows = new List<(string Modulo, string Metrica, string Valor)>
        {
            ("PDI", "Planos", report.Pdi.Total.ToString()),
            ("PDI", "Concluídos", report.Pdi.Completed.ToString()),
            ("PDI", "Por status", JoinCounts(report.Pdi.ByStatus)),
            ("Tarefas", "Total", report.Tasks.Total.ToString()),
            ("Tarefas", "Concluídas", report.Tasks.Completed.ToString()),
            ("Tarefas", "Atrasadas", report.Tasks.Overdue.ToString()),
            ("Tarefas", "Por estado", JoinCounts(report.Tasks.ByState)),
            ("Conhecimento", "Artigos", report.Knowledge.Total.ToString()),
            ("Conhecimento", "Publicados", report.Knowledge.Published.ToString()),
            ("Conhecimento", "Rascunhos", report.Knowledge.Draft.ToString()),
            ("Projetos", "Total", report.Projects.Total.ToString()),
            ("Projetos", "Por status", JoinCounts(report.Projects.ByStatus)),
            ("Incidentes", "Total", report.Incidents.Total.ToString()),
            ("Incidentes", "Resolvidos", report.Incidents.Resolved.ToString()),
            ("Incidentes", "Tempo médio (h)",
                report.Incidents.AverageResolutionHours?.ToString("F1") ?? "—"),
        };

        var row = 5;
        foreach (var (modulo, metrica, valor) in rows)
        {
            sheet.Cell(row, 1).Value = modulo;
            sheet.Cell(row, 2).Value = metrica;
            sheet.Cell(row, 3).Value = valor;
            row++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Block(
        IContainer container,
        string heading,
        (string Label, string Value)[] rows)
        => container.Column(column =>
        {
            column.Item().PaddingTop(10)
                .Text(text => text.Span(heading).FontSize(12).Bold());
            foreach (var (label, value) in rows)
                column.Item().Text(text =>
                {
                    text.Span($"{label}: ").Bold();
                    text.Span(value);
                });
        });

    private static string JoinCounts(IReadOnlyDictionary<string, int> counts)
        => counts.Count == 0
            ? "—"
            : string.Join(", ", counts.Select(kv => $"{kv.Key}={kv.Value}"));

    private static string Period(DateOnly? from, DateOnly? to)
        => $"{from?.ToString("dd/MM/yyyy") ?? "início"} – " +
           $"{to?.ToString("dd/MM/yyyy") ?? "hoje"}";
}

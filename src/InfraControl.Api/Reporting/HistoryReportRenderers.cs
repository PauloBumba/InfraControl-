using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using InfraControl.Application.Reports.History;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InfraControl.Api.Reporting;

/// <summary>
/// F25-T005 §14 — renderização da série histórica (docs/33 §13): CSV, PDF
/// (QuestPDF, licença Community) e XLSX (ClosedXML), no mesmo técnica dos
/// renderers do transversal (ADR-049) mas auto-contido.
/// </summary>
public static class HistoryReportRenderers
{
    static HistoryReportRenderers()
        => QuestPDF.Settings.License = LicenseType.Community;

    private static readonly string[] Headers =
        ["Mês", "Tasks", "Concluídas", "Knowledge", "PDIs"];

    public static byte[] RenderCsv(ReportHistoryDto history)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Headers));
        foreach (var m in history.Months)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                m.Month, m.Tasks.ToString(CultureInfo.InvariantCulture),
                m.CompletedTasks.ToString(CultureInfo.InvariantCulture),
                m.Knowledge.ToString(CultureInfo.InvariantCulture),
                m.Pdis.ToString(CultureInfo.InvariantCulture),
            }));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public static byte[] RenderPdf(ReportHistoryDto history)
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
                        text.Span("Histórico de Indicadores").FontSize(16).Bold());
                    column.Item().Text(text => text.Span(Period(history.From, history.To))
                        .FontSize(9).FontColor(Colors.Grey.Darken1));
                });
                page.Content().PaddingTop(16).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        foreach (var h in Headers)
                            header.Cell().Background(Colors.Grey.Lighten3)
                                .Padding(4).Text(h).Bold();
                    });
                    foreach (var m in history.Months)
                    {
                        foreach (var value in new[]
                        {
                            m.Month,
                            m.Tasks.ToString(CultureInfo.InvariantCulture),
                            m.CompletedTasks.ToString(CultureInfo.InvariantCulture),
                            m.Knowledge.ToString(CultureInfo.InvariantCulture),
                            m.Pdis.ToString(CultureInfo.InvariantCulture),
                        })
                            table.Cell().Padding(4).Text(value);
                    }
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("InfraControl · Histórico · página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

    public static byte[] RenderXlsx(ReportHistoryDto history)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Histórico");

        sheet.Cell(1, 1).Value = "Histórico de Indicadores";
        sheet.Cell(2, 1).Value = Period(history.From, history.To);
        for (var c = 0; c < Headers.Length; c++)
        {
            sheet.Cell(4, c + 1).Value = Headers[c];
            sheet.Cell(4, c + 1).Style.Font.Bold = true;
        }

        var row = 5;
        foreach (var m in history.Months)
        {
            sheet.Cell(row, 1).Value = m.Month;
            sheet.Cell(row, 2).Value = m.Tasks;
            sheet.Cell(row, 3).Value = m.CompletedTasks;
            sheet.Cell(row, 4).Value = m.Knowledge;
            sheet.Cell(row, 5).Value = m.Pdis;
            row++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string Period(DateOnly? from, DateOnly? to)
        => $"{from?.ToString("dd/MM/yyyy") ?? "início"} – " +
           $"{to?.ToString("dd/MM/yyyy") ?? "hoje"}";
}

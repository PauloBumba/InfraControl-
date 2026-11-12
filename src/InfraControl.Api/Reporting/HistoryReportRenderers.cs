using System.Globalization;
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
/// F52-T004: header/footer com <see cref="ReportBranding"/> (empresa +
/// logo + CNPJ + autoria); CSV ganha 1ª linha com a empresa.
/// </summary>
public static class HistoryReportRenderers
{
    static HistoryReportRenderers()
        => QuestPDF.Settings.License = LicenseType.Community;

    private static readonly string[] Headers =
        ["Mês", "Tasks", "Concluídas", "Knowledge", "PDIs"];

    public static byte[] RenderCsv(
        ReportHistoryDto history, ReportBranding? branding = null)
    {
        var b = branding ?? ReportBranding.Fallback();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(b.CsvCompanyLine);
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

        return ReportBranding.ToUtf8(sb.ToString());
    }

    public static byte[] RenderPdf(
        ReportHistoryDto history, ReportBranding? branding = null)
    {
        var b = branding ?? ReportBranding.Fallback();
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(text => text.FontSize(10));
                page.Header().Column(column =>
                {
                    ReportBrandingHeader.Render(column, b);
                    column.Item().PaddingTop(6).Text(text =>
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
                page.Footer().Column(footer =>
                {
                    ReportBrandingFooter.Render(footer, b, "Histórico");
                });
            });
        }).GeneratePdf();
    }

    public static byte[] RenderXlsx(
        ReportHistoryDto history, ReportBranding? branding = null)
    {
        var b = branding ?? ReportBranding.Fallback();
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Histórico");

        var nextRow = ReportBrandingXlsx.RenderHeader(sheet, b);
        sheet.Cell(nextRow, 1).Value = "Histórico de Indicadores";
        sheet.Cell(nextRow + 1, 1).Value = Period(history.From, history.To);
        var headerRow = nextRow + 3;
        for (var c = 0; c < Headers.Length; c++)
        {
            sheet.Cell(headerRow, c + 1).Value = Headers[c];
            sheet.Cell(headerRow, c + 1).Style.Font.Bold = true;
        }

        var row = headerRow + 1;
        foreach (var m in history.Months)
        {
            sheet.Cell(row, 1).Value = m.Month;
            sheet.Cell(row, 2).Value = m.Tasks;
            sheet.Cell(row, 3).Value = m.CompletedTasks;
            sheet.Cell(row, 4).Value = m.Knowledge;
            sheet.Cell(row, 5).Value = m.Pdis;
            row++;
        }

        ReportBrandingXlsx.RenderFooter(sheet, b, row + 1);
        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string Period(DateOnly? from, DateOnly? to)
        => $"{from?.ToString("dd/MM/yyyy") ?? "início"} – " +
           $"{to?.ToString("dd/MM/yyyy") ?? "hoje"}";
}

using System.Globalization;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InfraControl.Api.Reporting;

/// <summary>
/// F52-T004 — blocos compartilhados de <see cref="ReportBranding"/>:
/// cabeçalho PDF (logo + empresa + CNPJ), rodapé PDF (endereço/contato +
/// autoria + página N de M), cabeçalho/rodapé XLSX e linha de empresa do
/// CSV. Mantém os 3 conjuntos de renderers consistentes.
/// </summary>
internal static class ReportBrandingHeader
{
    /// <summary>Cabeçalho PDF: logo (≤36px) + nome/CNPJ + razão social.</summary>
    public static void Render(ColumnDescriptor column, ReportBranding b)
    {
        if (b.LogoBytes is { Length: > 0 } logo)
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(96).Height(36).AlignMiddle().AlignLeft()
                    .Image(logo).FitHeight();
                row.RelativeItem().Column(text =>
                {
                    text.Item().Text(b.CompanyLine).FontSize(10).Bold();
                    if (b.LegalLine is { } legal)
                        text.Item().Text(legal).FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                });
            });
        }
        else
        {
            column.Item().Text(b.CompanyLine).FontSize(10).Bold();
            if (b.LegalLine is { } legal)
                column.Item().Text(legal).FontSize(8)
                    .FontColor(Colors.Grey.Darken1);
        }
    }
}

internal static class ReportBrandingFooter
{
    /// <summary>
    /// Rodapé PDF: linha de informações (endereço · contato · gerado por)
    /// + numeração "página N de M" — centralizado como antes.
    /// </summary>
    public static void Render(ColumnDescriptor footer, ReportBranding b, string reportLabel)
    {
        if (!string.IsNullOrWhiteSpace(b.FooterInfoLine))
        {
            footer.Item().Text(b.FooterInfoLine)
                .FontSize(7).FontColor(Colors.Grey.Darken1);
        }
        footer.Item().AlignCenter().Text(text =>
        {
            text.Span($"{b.Name} · {reportLabel} · página ");
            text.CurrentPageNumber();
            text.Span(" de ");
            text.TotalPages();
        });
    }
}

internal static class ReportBrandingXlsx
{
    /// <summary>
    /// Cabeçalho XLSX: empresa (nome+CNPJ) na linha 1, razão social na 2,
    /// logo opcional ancorado em B1 (≤40px). Devolve a próxima linha livre.
    /// </summary>
    public static int RenderHeader(IXLWorksheet sheet, ReportBranding b)
    {
        sheet.Cell(1, 1).Value = b.CompanyLine;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Row(1).Height = 30;
        if (b.LegalLine is { } legal)
            sheet.Cell(2, 1).Value = legal;

        if (b.LogoBytes is { Length: > 0 } logo)
        {
            try
            {
                using var image = new MemoryStream(logo);
                var picture = sheet.AddPicture(image)
                    .MoveTo(sheet.Cell(1, 3))
                    .WithSize(120, 36);
                picture.Height = 36;
                picture.Width = 120;
            }
            catch
            {
                // Logo corrompido/formato não suportado — relatório sai sem ele.
            }
        }

        return 4;
    }

    /// <summary>Rodapé XLSX: linha de informações + autoria em linhas livres.</summary>
    public static void RenderFooter(IXLWorksheet sheet, ReportBranding b, int startRow)
    {
        if (string.IsNullOrWhiteSpace(b.FooterInfoLine))
            return;

        var row = startRow;
        sheet.Cell(row, 1).Value = b.FooterInfoLine;
        sheet.Cell(row, 1).Style.Font.Italic = true;
        sheet.Cell(row, 1).Style.Font.FontSize = 9;
        sheet.Cell(row + 1, 1).Value = b.GeneratedLine;
        sheet.Cell(row + 1, 1).Style.Font.Italic = true;
        sheet.Cell(row + 1, 1).Style.Font.FontSize = 9;
    }
}

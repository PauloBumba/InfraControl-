using ClosedXML.Excel;
using InfraControl.Application.Pdi;
using QuestPDF.Elements;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InfraControl.Api.Reporting;

/// <summary>
/// Renderização dos relatórios do PDI (docs/30 §22 — Fase 8):
/// PDF (QuestPDF, licença Community) e XLSX (ClosedXML). Cada método é
/// puro — recebe o DTO pronto do handler e devolve os bytes.
/// </summary>
public static class PdiReportRenderers
{
    static PdiReportRenderers()
        => QuestPDF.Settings.License = LicenseType.Community;

    // ---- Relatório individual (§22) ---------------------------------

    public static byte[] RenderIndividualPdf(PdiIndividualReportDto report)
        => Pdf(
            "Relatório Individual do PDI",
            $"{report.OwnerName} · {report.ManagerName ?? "Sem gestor"}",
            container => container.Column(column =>
            {
                Kv(column.Item(), "Dados", [
                    ("Colaborador", report.OwnerName),
                    ("E-mail", report.OwnerEmail),
                    ("Gestor", report.ManagerName ?? "—"),
                    ("Período", Period(report.PeriodStart, report.PeriodEnd)),
                    ("Status", report.Evaluation.Status),
                ]);

                column.Item().PaddingTop(10)
                    .Text(text => text.Span("Objetivos e fases")
                        .FontSize(12).Bold());
                foreach (var objective in report.Structure.Objectives)
                {
                    column.Item().PaddingTop(4).Text(text =>
                        text.Span(objective.Title).Bold().FontSize(11));
                    if (objective.CompetencyName is { } competency)
                        column.Item().Text(
                            $"Competência: {competency}");
                    foreach (var strategy in objective.Strategies)
                    {
                        column.Item().Text(
                            $"Estratégia: {strategy.Title}");
                        foreach (var phase in strategy.Phases)
                            column.Item().Text(
                                $"  {phase.Title} — {phase.Status} " +
                                $"({phase.Percent}%)");
                    }
                }

                Kv(column.Item(), "Acompanhamento", [
                    ("Check-ins",
                        report.Accompaniment.CheckIns.Count.ToString()),
                    ("Impedimentos",
                        report.Accompaniment.Impediments.Count.ToString()),
                    ("Evidências",
                        report.Accompaniment.Evidences.Count.ToString()),
                    ("Feedbacks",
                        report.Accompaniment.Feedbacks.Count.ToString()),
                ]);
                foreach (var checkIn in report.Accompaniment.CheckIns)
                    column.Item().Text(
                        $"Check-in {checkIn.CheckInDate:dd/MM/yyyy} — " +
                        checkIn.WhatDone);
                foreach (var impediment in report.Accompaniment.Impediments)
                    column.Item().Text(
                        $"Dificuldade: {impediment.Type} " +
                        $"({impediment.Status}) — {impediment.Description}");

                Kv(column.Item(), "Avaliação final", [
                    ("Autoavaliação",
                        report.Evaluation.SelfAssessment?.Content ?? "—"),
                    ("Avaliação do gestor",
                        report.Evaluation.ManagerAssessment?.Content ?? "—"),
                    ("Resultado",
                        report.Evaluation.Result is { } result
                            ? $"{result.Outcome} — {result.Notes ?? "—"}"
                            : "—"),
                ]);
            }));

    public static byte[] RenderIndividualXlsx(PdiIndividualReportDto report)
    {
        using var workbook = new XLWorkbook();

        var summary = workbook.Worksheets.Add("Resumo");
        var row = 1;
        foreach (var (label, value) in new[]
        {
            ("Relatório individual do PDI", (string?)null),
            ("Colaborador", report.OwnerName),
            ("E-mail", report.OwnerEmail),
            ("Gestor", report.ManagerName),
            ("Período", Period(report.PeriodStart, report.PeriodEnd)),
            ("Status", report.Evaluation.Status),
            ("Check-ins", report.Accompaniment.CheckIns.Count.ToString()),
            ("Impedimentos",
                report.Accompaniment.Impediments.Count.ToString()),
            ("Evidências", report.Accompaniment.Evidences.Count.ToString()),
            ("Feedbacks", report.Accompaniment.Feedbacks.Count.ToString()),
            ("Resultado", report.Evaluation.Result?.Outcome),
        })
        {
            summary.Cell(row, 1).Value = label;
            summary.Cell(row, 2).Value = value;
            row++;
        }
        summary.Columns().AdjustToContents();

        var structure = workbook.Worksheets.Add("Estrutura");
        structure.Cell(1, 1).Value = "Objetivo";
        structure.Cell(1, 2).Value = "Competência";
        structure.Cell(1, 3).Value = "Estratégia";
        structure.Cell(1, 4).Value = "Fase";
        structure.Cell(1, 5).Value = "Status";
        structure.Cell(1, 6).Value = "%";
        row = 2;
        foreach (var objective in report.Structure.Objectives)
        foreach (var strategy in objective.Strategies)
        foreach (var phase in strategy.Phases)
        {
            structure.Cell(row, 1).Value = objective.Title;
            structure.Cell(row, 2).Value = objective.CompetencyName;
            structure.Cell(row, 3).Value = strategy.Title;
            structure.Cell(row, 4).Value = phase.Title;
            structure.Cell(row, 5).Value = phase.Status;
            structure.Cell(row, 6).Value = phase.Percent;
            row++;
        }
        structure.Row(1).Style.Font.Bold = true;
        structure.Columns().AdjustToContents();

        var evaluation = workbook.Worksheets.Add("Avaliação");
        evaluation.Cell(1, 1).Value = "Campo";
        evaluation.Cell(1, 2).Value = "Conteúdo";
        evaluation.Row(1).Style.Font.Bold = true;
        evaluation.Cell(2, 1).Value = "Autoavaliação";
        evaluation.Cell(2, 2).Value =
            report.Evaluation.SelfAssessment?.Content;
        evaluation.Cell(3, 1).Value = "Avaliação do gestor";
        evaluation.Cell(3, 2).Value =
            report.Evaluation.ManagerAssessment?.Content;
        evaluation.Cell(4, 1).Value = "Resultado";
        evaluation.Cell(4, 2).Value =
            report.Evaluation.Result is { } result
                ? $"{result.Outcome} — {result.Notes}"
                : null;
        evaluation.Columns().AdjustToContents();

        return Save(workbook);
    }

    // ---- Relatório semestral (§22) ----------------------------------

    public static byte[] RenderSemesterPdf(PdiSemesterReportDto report)
        => Pdf(
            "Relatório Semestral do PDI",
            $"{report.Label} · {Period(report.From, report.To)}",
            container => container.Column(column =>
            {
                Kv(column.Item(), "Consolidado", [
                    ("Planos no período", report.PlansInPeriod.ToString()),
                    ("Planos criados no período",
                        report.PlansCreatedInPeriod.ToString()),
                    ("Concluídos", report.CompletedInPeriod.ToString()),
                    ("Taxa de conclusão",
                        $"{report.CompletionRatePercent}%"),
                    ("Dias médios até concluir",
                        report.AvgCompletionDays?.ToString() ?? "—"),
                ]);
                Kv(column.Item(), "Tendências (vs. janela anterior)", [
                    ("Planos anteriores",
                        report.Trends.PreviousPlansInPeriod.ToString()),
                    ("Variação de planos",
                        $"{report.Trends.PlansDelta:+0;-0;0}"),
                    ("Variação de concluídos",
                        $"{report.Trends.CompletedDelta:+0;-0;0}"),
                    ("Variação da taxa",
                        $"{report.Trends.CompletionRateDelta:+0;-0;0} p.p."),
                ]);

                column.Item().PaddingTop(10)
                    .Text(text => text.Span("Gestores")
                        .FontSize(12).Bold());
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        HeaderCell(header, "Gestor");
                        HeaderCell(header, "Planos");
                        HeaderCell(header, "Concluídos");
                    });
                    foreach (var manager in report.Managers)
                    {
                        table.Cell().Text(manager.ManagerName);
                        table.Cell().Text(manager.Plans.ToString());
                        table.Cell().Text(manager.Completed.ToString());
                    }
                });

                column.Item().PaddingTop(10)
                    .Text(text => text.Span("Competências")
                        .FontSize(12).Bold());
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        HeaderCell(header, "Competência");
                        HeaderCell(header, "Objetivos");
                        HeaderCell(header, "% médio");
                    });
                    foreach (var competency in report.Competencies)
                    {
                        table.Cell().Text(competency.Name);
                        table.Cell().Text(competency.Objectives.ToString());
                        table.Cell().Text(
                            $"{competency.AvgPhasePercent}%");
                    }
                });

                column.Item().PaddingTop(10)
                    .Text(text => text.Span("Dificuldades")
                        .FontSize(12).Bold());
                foreach (var share in report.Difficulties)
                    column.Item().Text(
                        $"{share.Type}: {share.Count} ({share.Percent}%)");
            }));

    public static byte[] RenderSemesterXlsx(PdiSemesterReportDto report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Resumo");

        var row = 1;
        void Put(string label, string? value)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 2).Value = value;
            row++;
        }

        Put("Relatório semestral", report.Label);
        Put("Período", Period(report.From, report.To));
        Put("Planos no período", report.PlansInPeriod.ToString());
        Put("Planos criados", report.PlansCreatedInPeriod.ToString());
        Put("Concluídos", report.CompletedInPeriod.ToString());
        Put("Taxa de conclusão",
            $"{report.CompletionRatePercent}%");
        Put("Dias médios", report.AvgCompletionDays?.ToString());
        Put("Planos anteriores",
            report.Trends.PreviousPlansInPeriod.ToString());
        Put("Variação de planos",
            $"{report.Trends.PlansDelta:+0;-0;0}");
        Put("Variação de concluídos",
            $"{report.Trends.CompletedDelta:+0;-0;0}");
        Put("Variação da taxa",
            $"{report.Trends.CompletionRateDelta:+0;-0;0} p.p.");

        row++;
        Put("Gestor", "Planos / concluídos");
        foreach (var manager in report.Managers)
            Put(manager.ManagerName,
                $"{manager.Plans} / {manager.Completed}");

        row++;
        Put("Competência", "Objetivos / % médio");
        foreach (var competency in report.Competencies)
            Put(competency.Name,
                $"{competency.Objectives} / {competency.AvgPhasePercent}%");

        row++;
        Put("Dificuldade", "Contagem / percentual");
        foreach (var share in report.Difficulties)
            Put(share.Type, $"{share.Count} / {share.Percent}%");

        sheet.Columns().AdjustToContents();
        return Save(workbook);
    }

    // ---- Relatório de dificuldades (§22) -----------------------------

    public static byte[] RenderDifficultiesPdf(
        PdiDifficultiesReportDto report)
        => Pdf(
            "Relatório de Dificuldades do PDI",
            Period(report.From, report.To),
            container => container.Column(column =>
            {
                Kv(column.Item(), "Consolidado", [
                    ("Dificuldades no período", report.Total.ToString()),
                    ("Resolvidas", report.Resolved.ToString()),
                    ("Dias médios p/ resolver",
                        report.AvgResolutionDays?.ToString() ?? "—"),
                ]);

                column.Item().PaddingTop(10)
                    .Text(text => text.Span("Por tipo")
                        .FontSize(12).Bold());
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        HeaderCell(header, "Tipo");
                        HeaderCell(header, "Quantidade");
                        HeaderCell(header, "Percentual");
                    });
                    foreach (var share in report.ByType)
                    {
                        table.Cell().Text(share.Type);
                        table.Cell().Text(share.Count.ToString());
                        table.Cell().Text($"{share.Percent}%");
                    }
                });
            }));

    public static byte[] RenderDifficultiesXlsx(
        PdiDifficultiesReportDto report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Dificuldades");

        sheet.Cell(1, 1).Value = "Tipo";
        sheet.Cell(1, 2).Value = "Quantidade";
        sheet.Cell(1, 3).Value = "Percentual";
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var share in report.ByType)
        {
            sheet.Cell(row, 1).Value = share.Type;
            sheet.Cell(row, 2).Value = share.Count;
            sheet.Cell(row, 3).Value = $"{share.Percent}%";
            row++;
        }

        row++;
        sheet.Cell(row, 1).Value = "Total";
        sheet.Cell(row, 2).Value = report.Total;
        sheet.Cell(row + 1, 1).Value = "Resolvidas";
        sheet.Cell(row + 1, 2).Value = report.Resolved;
        sheet.Cell(row + 2, 1).Value = "Dias médios p/ resolver";
        sheet.Cell(row + 2, 2).Value = report.AvgResolutionDays;

        sheet.Columns().AdjustToContents();
        return Save(workbook);
    }

    // ---- Helpers -----------------------------------------------------

    private static byte[] Pdf(
        string title,
        string subtitle,
        Action<IContainer> body)
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
                        text.Span(title).FontSize(16).Bold());
                    column.Item().Text(text => text.Span(subtitle)
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken1));
                });
                page.Content().PaddingTop(16).Element(body);
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("InfraControl · PDI · página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

    /// <summary>Mini-bloco "rótulo: valor" (o container vira uma coluna própria).</summary>
    private static void Kv(
        IContainer container,
        string heading,
        (string Label, string? Value)[] rows)
        => container.Column(column =>
        {
            column.Item().PaddingTop(10)
                .Text(text => text.Span(heading).FontSize(12).Bold());
            foreach (var (label, value) in rows)
                column.Item().Text(text =>
                {
                    text.Span($"{label}: ").Bold();
                    text.Span(value ?? "—");
                });
        });

    /// <summary>Célula de cabeçalho cinza (o tipo do header é interno no QuestPDF).</summary>
    private static void HeaderCell(
        QuestPDF.Fluent.TableCellDescriptor header, string title)
        => header.Cell()
            .Background(Colors.Grey.Lighten3)
            .Padding(2)
            .Text(text => text.Span(title).Bold());

    private static string Period(DateOnly? from, DateOnly? to)
        => $"{from?.ToString("dd/MM/yyyy") ?? "início"} – " +
           $"{to?.ToString("dd/MM/yyyy") ?? "hoje"}";

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

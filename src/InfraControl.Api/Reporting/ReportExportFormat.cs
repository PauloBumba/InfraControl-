namespace InfraControl.Api.Reporting;

/// <summary>
/// F25-T005 §14 — formato de exportação dos relatórios: <c>csv</c>,
/// <c>pdf</c> (padrão) ou <c>xlsx</c>; valor inválido = 400
/// (ArgumentException no GlobalExceptionHandler).
/// </summary>
public readonly record struct ReportExportFormat(
    string Value,
    string ContentType,
    string Extension)
{
    public static readonly ReportExportFormat Csv = new(
        "csv", "text/csv", "csv");

    public static readonly ReportExportFormat Pdf = new(
        "pdf", "application/pdf", "pdf");

    public static readonly ReportExportFormat Xlsx = new(
        "xlsx",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "xlsx");

    /// <summary>Nulo/vazio = PDF; texto fora de csv|pdf|xlsx = 400.</summary>
    public static ReportExportFormat Parse(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return Pdf;

        return format.Trim().ToLowerInvariant() switch
        {
            "csv" => Csv,
            "pdf" => Pdf,
            "xlsx" => Xlsx,
            _ => throw new ArgumentException(
                $"'{format}' não é um formato de exportação válido " +
                "— use csv, pdf ou xlsx.")
        };
    }
}

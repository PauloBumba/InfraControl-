namespace InfraControl.Api.Reporting;

/// <summary>
/// Formato de exportação dos relatórios do PDI (docs/30 §22 — Fase 8):
/// <c>pdf</c> (padrão) ou <c>xlsx</c>; valor inválido = 400
/// (ArgumentException no GlobalExceptionHandler).
/// </summary>
public readonly record struct PdiExportFormat(
    string Value,
    string ContentType,
    string Extension)
{
    public static readonly PdiExportFormat Pdf = new(
        "pdf", "application/pdf", "pdf");

    public static readonly PdiExportFormat Xlsx = new(
        "xlsx",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "xlsx");

    /// <summary>Nulo/vazio = PDF; texto fora de pdf|xlsx = 400.</summary>
    public static PdiExportFormat Parse(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return Pdf;

        return format.Trim().ToLowerInvariant() switch
        {
            "pdf" => Pdf,
            "xlsx" => Xlsx,
            _ => throw new ArgumentException(
                $"'{format}' não é um formato de exportação válido " +
                "— use pdf ou xlsx.")
        };
    }
}

using System.Text;
using InfraControl.Application.Company;

namespace InfraControl.Api.Reporting;

/// <summary>
/// F52-T004 — identidade visual dos relatórios exportados (padrão
/// ServiceNow/ERP): dados da empresa (<see cref="CompanySettingsDto"/>),
/// logo em bytes (lido do disco, tolera ausente) e autoria do export
/// (<see cref="GeneratedBy"/> + <see cref="GeneratedAt"/>).
/// Os renderers recebem este objeto no lugar do antigo
/// <c>string? companyName</c> — fallback "InfraControl" quando não há
/// empresa configurada.
/// </summary>
public sealed record ReportBranding(
    string Name,
    string? LegalName,
    string? Cnpj,
    byte[]? LogoBytes,
    string? Address,
    string? Contact,
    string? GeneratedBy,
    DateTime GeneratedAt)
{
    public const string FallbackName = "InfraControl";

    /// <summary>Sem empresa configurada — marca da plataforma.</summary>
    public static ReportBranding Fallback(
        string? generatedBy = null, DateTime? generatedAt = null)
        => new(FallbackName, null, null, null, null, null,
            generatedBy, generatedAt ?? DateTime.UtcNow);

    /// <summary>Monta o branding a partir do DTO de empresa + autoria.</summary>
    public static ReportBranding From(
        CompanySettingsDto? settings,
        string? generatedBy = null,
        byte[]? logoBytes = null,
        DateTime? generatedAt = null)
    {
        if (settings is null)
            return Fallback(generatedBy, generatedAt);

        return new ReportBranding(
            string.IsNullOrWhiteSpace(settings.Name) ? FallbackName : settings.Name.Trim(),
            settings.LegalName,
            settings.Cnpj,
            logoBytes,
            AddressLine(settings),
            ContactLine(settings),
            generatedBy,
            generatedAt ?? DateTime.UtcNow);
    }

    /// <summary>Linha do cabeçalho: nome + CNPJ (quando houver).</summary>
    public string CompanyLine
        => string.IsNullOrWhiteSpace(Cnpj)
            ? Name
            : $"{Name} · CNPJ {Cnpj}";

    /// <summary>Razão social (só quando difere do nome fantasia).</summary>
    public string? LegalLine
        => string.IsNullOrWhiteSpace(LegalName) || LegalName.Trim() == Name
            ? null
            : LegalName.Trim();

    /// <summary>Rodapé: endereço/contato + autoria + página (composição dos renderers).</summary>
    public string FooterInfoLine
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Address)) parts.Add(Address);
            if (!string.IsNullOrWhiteSpace(Contact)) parts.Add(Contact);
            parts.Add(GeneratedLine);
            return string.Join("  ·  ", parts);
        }
    }

    public string GeneratedLine
        => $"Gerado por {GeneratedBy ?? "—"} em {GeneratedAt:dd/MM/yyyy HH:mm}";

    /// <summary>Linha de empresa para CSV (1ª linha do arquivo).</summary>
    public string CsvCompanyLine
        => string.IsNullOrWhiteSpace(LegalName)
            ? CompanyLine
            : $"{CompanyLine} — {LegalName.Trim()}";

    private static string? AddressLine(CompanySettingsDto s)
    {
        var street = string.Join(", ",
            new[] { s.Street, s.Number, s.Complement, s.District }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!.Trim()));
        var cityState = string.Join(" - ",
            new[] { s.City, s.State }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!.Trim()));
        var city = string.IsNullOrWhiteSpace(s.ZipCode)
            ? cityState
            : string.Join(", ",
                new[] { cityState, s.ZipCode.Trim() }
                    .Where(p => p.Length > 0));
        var line = string.Join(", ",
            new[] { street, city }.Where(p => p.Length > 0));
        return line.Length == 0 ? null : line;
    }

    private static string? ContactLine(CompanySettingsDto s)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(s.Phone)) parts.Add($"Tel {s.Phone.Trim()}");
        if (!string.IsNullOrWhiteSpace(s.Email)) parts.Add(s.Email.Trim());
        if (!string.IsNullOrWhiteSpace(s.Website)) parts.Add(s.Website.Trim());
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>Texto de auditoria da exportação (resource Action).</summary>
    public static string AuditAction(string reportSlug, string format)
        => $"ReportExport.{reportSlug}.{format}";

    /// <summary>Encoding compartilhado dos CSVs de relatório.</summary>
    public static byte[] ToUtf8(string text) => Encoding.UTF8.GetBytes(text);
}

using Microsoft.AspNetCore.Hosting;

namespace InfraControl.Api.Reporting;

/// <summary>
/// F52-T004 — resolve o <see cref="CompanySettingsDto.LogoPath"/>
/// (path web <c>/uploads/…</c>) para bytes em disco. O arquivo vive em
/// <c>{WebRoot}/uploads/…</c> (mesmo diretório do <c>UseStaticFiles</c>);
/// logo ausente, path fora de <c>uploads/</c> ou erro de leitura → null
/// (o relatório sai sem logo, nunca falha por branding).
/// </summary>
public static class ReportBrandingLogo
{
    public static byte[]? Load(IWebHostEnvironment environment, string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath))
            return null;

        var relative = logoPath.Trim();
        if (!relative.StartsWith('/') && !relative.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            relative = "/" + relative;
        if (!relative.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return null;

        var webRoot = Path.GetFullPath(environment.WebRootPath
            ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
        var fullPath = Path.GetFullPath(Path.Combine(webRoot, relative.TrimStart('/')));
        if (!fullPath.StartsWith(webRoot, StringComparison.Ordinal))
            return null;

        try
        {
            return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}

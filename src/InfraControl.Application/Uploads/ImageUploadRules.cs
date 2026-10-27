namespace InfraControl.Application.Uploads;

/// <summary>
/// F24-T008 — regras reutilizáveis de upload de imagem (allowlist de
/// extensão + MIME + teto de tamanho). Qualquer módulo (Knowledge, etc.)
/// reusa estas constantes; fora daqui = 400 pt-BR.
/// </summary>
public static class ImageUploadRules
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>Módulos permitidos como subpasta sob <c>uploads/</c>.</summary>
    public static readonly string[] AllowedModules =
    [
        "knowledge",
        "general",
    ];

    /// <summary>
    /// Extensão → Content-Type canônico. A extensão original é preservada
    /// no nome em disco (<c>{guid}{ext}</c>).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ExtensionToContentType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".bmp"] = "image/bmp",
            [".svg"] = "image/svg+xml",
            [".avif"] = "image/avif",
        };

    public static IReadOnlyCollection<string> AllowedExtensions =>
        ExtensionToContentType.Keys.ToArray();

    public static string NormalizeModule(string? module)
    {
        var value = string.IsNullOrWhiteSpace(module) ? "general" : module.Trim().ToLowerInvariant();
        if (!AllowedModules.Contains(value, StringComparer.Ordinal))
            throw new ArgumentException(
                $"Módulo de upload inválido. Aceitos: {string.Join(", ", AllowedModules)}.",
                nameof(module));
        return value;
    }

    public static string NormalizeExtension(string? fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) ||
            !ExtensionToContentType.ContainsKey(extension))
        {
            throw new ArgumentException(
                $"Tipo de arquivo não permitido. Aceitos: {string.Join(", ", AllowedExtensions)}.",
                nameof(fileName));
        }

        return extension;
    }

    public static void EnsureAllowed(
        string? fileName,
        string? contentType,
        long length)
    {
        if (length <= 0)
            throw new ArgumentException("Nenhum arquivo enviado.", nameof(length));

        if (length > MaxFileSizeBytes)
            throw new ArgumentException(
                $"O arquivo pode ter no máximo {MaxFileSizeBytes / (1024 * 1024)} MB.",
                nameof(length));

        var extension = NormalizeExtension(fileName);
        var expected = ExtensionToContentType[extension];

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var normalized = contentType.Split(';', 2)[0].Trim();
            var ok = string.Equals(normalized, expected, StringComparison.OrdinalIgnoreCase)
                     || (normalized.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                         && ExtensionToContentType.Values.Contains(
                             normalized, StringComparer.OrdinalIgnoreCase));
            if (!ok)
                throw new ArgumentException(
                    $"Tipo de conteúdo não permitido para {extension}. Esperado: {expected}.",
                    nameof(contentType));
        }
    }
}

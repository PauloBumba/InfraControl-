using InfraControl.Application.Uploads;
using InfraControl.Application.Uploads.Ports;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// F24-T008 — imagens em <c>{ContentRoot}/wwwroot/uploads/{module}/</c>
/// (servidas via <c>UseStaticFiles</c>). Nome em disco = GUID + extensão
/// validada — a extensão original é preservada.
/// </summary>
public sealed class PhysicalImageUploadStore : IImageUploadStore
{
    private readonly string _root;

    public PhysicalImageUploadStore(
        IOptions<StorageOptions> options,
        IHostEnvironment environment)
    {
        var configured = options.Value.ImageUploadsPath;
        _root = Path.IsPathRooted(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configured));
    }

    public async Task<string> SaveAsync(
        string module,
        string extension,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        module = ImageUploadRules.NormalizeModule(module);
        extension = extension?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!ImageUploadRules.ExtensionToContentType.ContainsKey(extension))
            throw new ArgumentException(
                $"Tipo de arquivo não permitido. Aceitos: {string.Join(", ", ImageUploadRules.AllowedExtensions)}.",
                nameof(extension));

        var folder = Path.Combine(_root, module);
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.GetFullPath(Path.Combine(folder, fileName));
        if (!fullPath.StartsWith(Path.GetFullPath(folder), StringComparison.Ordinal))
            throw new InvalidOperationException("Caminho de upload inválido.");

        try
        {
            await using var target = new FileStream(
                fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 81920, useAsync: true);
            await content.CopyToAsync(target, cancellationToken);
        }
        catch
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            throw;
        }

        return $"/uploads/{module}/{fileName}";
    }
}

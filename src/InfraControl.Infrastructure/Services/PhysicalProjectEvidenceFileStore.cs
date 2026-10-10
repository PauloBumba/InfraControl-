using InfraControl.Application.Projects.Ports;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Storage físico das evidências de projeto em disco local.
/// </summary>
public sealed class PhysicalProjectEvidenceFileStore(
    IOptions<ProjectEvidenceOptions> options)
    : IProjectEvidenceFileStore
{
    private readonly string _root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, options.Value.StoragePath));

    public async Task SaveAsync(
        Guid evidenceId, string extension, Stream content,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        var path = PathFor(evidenceId, extension);

        try
        {
            await using var target = new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 81920, useAsync: true);
            await content.CopyToAsync(target, cancellationToken);
        }
        catch
        {
            if (File.Exists(path))
                File.Delete(path);
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(
        Guid evidenceId, string extension,
        CancellationToken cancellationToken = default)
    {
        var path = PathFor(evidenceId, extension);
        if (!File.Exists(path))
            throw new KeyNotFoundException(
                $"Arquivo da evidência '{evidenceId}' não encontrado.");

        Stream stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(
        Guid evidenceId, string extension,
        CancellationToken cancellationToken = default)
    {
        var path = PathFor(evidenceId, extension);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string PathFor(Guid evidenceId, string extension)
    {
        extension = extension?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("A extensão da evidência é obrigatória.", nameof(extension));

        var path = Path.GetFullPath(Path.Combine(_root, $"{evidenceId}{extension}"));
        if (!path.StartsWith(_root, StringComparison.Ordinal))
            throw new InvalidOperationException("Caminho de arquivo de evidência inválido.");

        return path;
    }
}

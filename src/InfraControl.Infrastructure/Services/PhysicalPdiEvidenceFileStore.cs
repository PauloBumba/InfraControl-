using InfraControl.Application.Pdi.Ports;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Onde os arquivos de evidência moram (docs/30 §12, Fase 4 — ADR-034):
/// caminho relativo à base da aplicação (padrão dev/containers; storage
/// externo continua fora do escopo).
/// </summary>
public sealed class PdiEvidenceOptions
{
    public const string SectionName = "PdiEvidence";

    public string StoragePath { get; set; } = "var/pdi-evidence";
}

/// <summary>
/// Storage em disco dos arquivos de evidência. O nome é sempre
/// <c>{evidenceId}{extension}</c> com a extensão saneada no domínio —
/// nada de nome/caminho do usuário chega aqui.
/// </summary>
public sealed class PhysicalPdiEvidenceFileStore(
    IOptions<PdiEvidenceOptions> options)
    : IPdiEvidenceFileStore
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
            // Escrita parcial/cancelada não pode deixar arquivo quebrado.
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
        var path = Path.GetFullPath(Path.Combine(_root, $"{evidenceId}{extension}"));

        // Defesa em profundidade: a extensão veio saneada do domínio,
        // mas nunca custa garantir que o resultado ficou sob a raiz.
        if (!path.StartsWith(_root, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Caminho de arquivo de evidência inválido.");

        return path;
    }
}

using InfraControl.Application.Tasks.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Evidências em disco local (F23-T004/P4c — ADR-033): pasta
/// `Storage:EvidencePath`, arquivo nomeado como `StoredName` (GUID +
/// extensão validada no domínio — nunca o nome do usuário, então não há
/// travessia de diretório por construção; a porta revalida mesmo assim).
/// </summary>
public sealed class LocalEvidenceFileStore(
    IOptions<StorageOptions> options,
    ILogger<LocalEvidenceFileStore> logger) : IEvidenceFileStore
{
    private string Root => Path.GetFullPath(options.Value.EvidencePath);

    public async Task SaveAsync(
        string storedName, byte[] content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Root);
        await File.WriteAllBytesAsync(Resolve(storedName), content, cancellationToken);
    }

    public async Task<byte[]> ReadAsync(
        string storedName, CancellationToken cancellationToken = default)
    {
        var path = Resolve(storedName);
        if (!File.Exists(path))
            throw new KeyNotFoundException(
                $"Arquivo '{storedName}' ausente no armazenamento.");

        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    public void Delete(string storedName)
    {
        try
        {
            var path = Resolve(storedName);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Banco já é fonte de verdade (registro excluído); um resíduo
            // em disco não pode derrubar a request — loga e segue (ADR-033).
            logger.LogWarning(
                ex, "Falha ao excluir arquivo de evidência {StoredName}.", storedName);
        }
    }

    /// <summary>Guard de defesa: storedName é GUID+ext gerado pelo domínio.</summary>
    private string Resolve(string storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName) ||
            !string.Equals(Path.GetFileName(storedName), storedName, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Nome de arquivo de armazenamento inválido.", nameof(storedName));
        }

        return Path.Combine(Root, storedName);
    }
}

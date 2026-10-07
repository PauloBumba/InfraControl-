namespace InfraControl.Application.Tasks.Ports;

/// <summary>
/// Armazenamento binário das evidências (F23-T004/P4c — ADR-033):
/// implementação atual = disco local (`LocalEvidenceFileStore`); trocar
/// o backend = outra implementação desta porta, sem tocar nos handlers.
/// </summary>
public interface IEvidenceFileStore
{
    Task SaveAsync(
        string storedName, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>Arquivo ausente no disco → KeyNotFoundException (404 pt-BR).</summary>
    Task<byte[]> ReadAsync(
        string storedName, CancellationToken cancellationToken = default);

    /// <summary>Erro de IO é logado e engolido — não bloqueia a exclusão do registro.</summary>
    void Delete(string storedName);
}

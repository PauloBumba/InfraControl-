namespace InfraControl.Application.Pdi.Ports;

/// <summary>
/// Storage dos arquivos de evidência (docs/30 §12, Fase 4 — ADR-034):
/// a entidade guarda só metadados, o binário mora aqui. O nome no
/// storage é sempre <c>{evidenceId}{extension}</c> — sem colisão e sem
/// caminho vindo do usuário.
/// </summary>
public interface IPdiEvidenceFileStore
{
    Task SaveAsync(
        Guid evidenceId, string extension, Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        Guid evidenceId, string extension,
        CancellationToken cancellationToken = default);

    /// <summary>Remoção best-effort (rollback quando o save do banco falha).</summary>
    Task DeleteAsync(
        Guid evidenceId, string extension,
        CancellationToken cancellationToken = default);
}

namespace InfraControl.Application.Projects.Ports;

/// <summary>
/// Storage das evidências do Projeto (ADR-043 · ADR-033/034):
/// binário em disco <c>var/project-evidence</c>; nome = <c>{id}{ext}</c>.
/// </summary>
public interface IProjectEvidenceFileStore
{
    Task SaveAsync(
        Guid evidenceId, string extension, Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        Guid evidenceId, string extension,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid evidenceId, string extension,
        CancellationToken cancellationToken = default);
}

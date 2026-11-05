namespace InfraControl.Application.Uploads.Ports;

/// <summary>
/// F24-T008 — storage reutilizável de imagens em disco
/// (<c>wwwroot/uploads/{module}/{guid}{ext}</c>). Preserva a extensão
/// original; o path web relativo é o contrato para o frontend.
/// </summary>
public interface IImageUploadStore
{
    /// <summary>
    /// Salva o stream e devolve o path web (ex.:
    /// <c>/uploads/knowledge/{guid}.png</c>).
    /// </summary>
    Task<string> SaveAsync(
        string module,
        string extension,
        Stream content,
        CancellationToken cancellationToken = default);
}

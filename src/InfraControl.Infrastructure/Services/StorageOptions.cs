namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Armazenamento de arquivos (F23-T004/P4c — ADR-033): seção `Storage`
/// do appsettings. Caminho absoluto ou relativo à pasta da aplicação.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Pasta das evidências (default: `evidence-files`).</summary>
    public string EvidencePath { get; set; } = "evidence-files";

    /// <summary>
    /// F24-T008 — pasta das imagens (relativa ao cwd da API; default
    /// <c>wwwroot/uploads</c>). Subpastas por módulo (knowledge, general…).
    /// </summary>
    public string ImageUploadsPath { get; set; } = Path.Combine("wwwroot", "uploads");
}

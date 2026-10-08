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
}

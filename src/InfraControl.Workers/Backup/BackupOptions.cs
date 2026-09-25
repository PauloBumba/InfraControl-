namespace InfraControl.Workers.Backup;

/// <summary>
/// Configuração do backup periódico do PostgreSQL (F12-T003,
/// docs/operations/BACKUP-RESTORE.md) — seção "Backup". A senha nunca
/// aparece aqui: vem do <c>ConnectionString</c> (env
/// <c>ConnectionStrings__DefaultConnection</c> por padrão).
/// </summary>
public sealed record BackupOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalHours { get; set; } = 24;
    public int RetentionDays { get; set; } = 7;
    public string Path { get; set; } = "backups";
    public string FileNamePrefix { get; set; } = "infracontrol";
    public int TimeoutSeconds { get; set; } = 3600;
    public string ConnectionString { get; set; } = string.Empty;
}

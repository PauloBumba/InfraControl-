namespace InfraControl.Workers.Backup;

/// <summary>
/// Alvo adicional de backup (F34-T003, ADR-060) — banco do gateway de
/// acesso remoto incluído no mesmo <c>BackupWorker</c> que já protege o
/// PostgreSQL principal. A senha nunca aparece aqui: vem da
/// <c>ConnectionString</c> (env <c>Backup__AdditionalTargets__0__*</c>).
/// </summary>
public sealed record BackupTargetOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string FileNamePrefix { get; set; } = string.Empty;
}

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

    /// <summary>
    /// Bancos extras dumpados na mesma execução (F34-T003: banco do
    /// gateway Guacamole). Vazio por default — comportamento do F12-T003
    /// intacto. Cada alvo tem prefixo próprio e heartbeat
    /// <c>latest-{prefix}.json</c>.
    /// </summary>
    public List<BackupTargetOptions> AdditionalTargets { get; set; } = [];
}

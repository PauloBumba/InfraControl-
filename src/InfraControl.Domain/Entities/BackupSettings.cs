using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Configuração operacional editável pela plataforma (spec evolução §3/§9):
/// singleton que sobrepõe <c>Backup:*</c> do appsettings em runtime —
/// mesma técnica do <see cref="CompanySettings"/> e do RetentionPolicy.
/// A senha nunca aparece aqui: continua vindo da ConnectionString.
/// </summary>
public sealed class BackupSettings : Entity
{
    public const int PathMaxLength = 260;
    public const int PrefixMaxLength = 80;

    public bool Enabled { get; private set; } = true;
    public int IntervalHours { get; private set; } = 24;
    public int RetentionDays { get; private set; } = 7;
    public string Path { get; private set; } = "backups";
    public string FileNamePrefix { get; private set; } = "infracontrol";
    public int TimeoutSeconds { get; private set; } = 3600;

    private BackupSettings() { }

    public static BackupSettings Create(
        bool enabled, int intervalHours, int retentionDays,
        string path, string fileNamePrefix, int timeoutSeconds)
    {
        var settings = new BackupSettings();
        settings.Apply(enabled, intervalHours, retentionDays, path, fileNamePrefix, timeoutSeconds);
        return settings;
    }

    public void Update(
        bool enabled, int intervalHours, int retentionDays,
        string path, string fileNamePrefix, int timeoutSeconds) =>
        Apply(enabled, intervalHours, retentionDays, path, fileNamePrefix, timeoutSeconds);

    private void Apply(
        bool enabled, int intervalHours, int retentionDays,
        string path, string fileNamePrefix, int timeoutSeconds)
    {
        if (intervalHours is < 1 or > 720)
            throw new ArgumentException(
                "O intervalo do backup deve estar entre 1 e 720 horas.", nameof(intervalHours));
        if (retentionDays is < 1 or > 3650)
            throw new ArgumentException(
                "A retenção do backup deve estar entre 1 e 3650 dias.", nameof(retentionDays));
        if (timeoutSeconds is < 60 or > 86400)
            throw new ArgumentException(
                "O timeout do backup deve estar entre 60 e 86400 segundos.", nameof(timeoutSeconds));

        var normalizedPath = (path ?? string.Empty).Trim();
        if (normalizedPath.Length == 0 || normalizedPath.Length > PathMaxLength)
            throw new ArgumentException(
                $"O diretório de backup deve ter entre 1 e {PathMaxLength} caracteres.",
                nameof(path));

        var normalizedPrefix = (fileNamePrefix ?? string.Empty).Trim();
        if (normalizedPrefix.Length == 0 || normalizedPrefix.Length > PrefixMaxLength)
            throw new ArgumentException(
                $"O prefixo do arquivo deve ter entre 1 e {PrefixMaxLength} caracteres.",
                nameof(fileNamePrefix));

        Enabled = enabled;
        IntervalHours = intervalHours;
        RetentionDays = retentionDays;
        Path = normalizedPath;
        FileNamePrefix = normalizedPrefix;
        TimeoutSeconds = timeoutSeconds;
    }
}

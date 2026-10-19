using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace InfraControl.Workers.Backup;

/// <summary>
/// Resultado de uma execução de backup (F12-T003).
/// </summary>
public sealed record BackupResult(
    bool Success,
    string? FileName,
    long? SizeBytes,
    int Pruned,
    string? Error);

/// <summary>
/// Executa um <c>pg_dump -Fc</c> periódico no diretório configurado,
/// escreve o status <c>latest.json</c> e apaga backups além da retenção.
/// A senha vai só no ambiente (<c>PGPASSWORD</c>), nunca em argv.
/// </summary>
public sealed class PostgresBackupService(
    IProcessRunner runner,
    IOptions<BackupOptions> options,
    ILogger<PostgresBackupService> logger)
{
    public async Task<BackupResult> RunOnceAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        Directory.CreateDirectory(settings.Path);

        // Alvo principal: comportamento do F12-T003 preservado byte a byte
        // (mesmos nomes de arquivo, latest.json, mensagens e poda).
        var primary = await RunTargetAsync(
            settings.ConnectionString,
            settings.FileNamePrefix,
            "latest.json",
            settings,
            cancellationToken);

        var results = new List<BackupResult> { primary };

        // Alvos adicionais (F34-T003): banco do gateway de acesso remoto.
        // Falha em um alvo não impede os demais — cada um tem dump e
        // heartbeat próprios; o resultado agregado só falha se QUALQUER
        // alvo falhar (o worker loga o motivo de todos).
        foreach (var target in settings.AdditionalTargets)
        {
            if (string.IsNullOrWhiteSpace(target.ConnectionString))
                continue;

            var prefix = string.IsNullOrWhiteSpace(target.FileNamePrefix)
                ? "backup"
                : target.FileNamePrefix;

            results.Add(await RunTargetAsync(
                target.ConnectionString,
                prefix,
                $"latest-{prefix}.json",
                settings,
                cancellationToken));
        }

        if (results.Count == 1)
            return primary;

        var errors = results
            .Where(result => !result.Success)
            .Select(result => result.Error!)
            .ToList();

        return new BackupResult(
            errors.Count == 0,
            primary.FileName,
            primary.SizeBytes,
            results.Sum(result => result.Pruned),
            errors.Count == 0 ? null : string.Join(" ", errors));
    }

    private async Task<BackupResult> RunTargetAsync(
        string connectionString,
        string prefix,
        string statusFileName,
        BackupOptions settings,
        CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnectionStringBuilder(connectionString);

        Directory.CreateDirectory(settings.Path);

        var utcNow = DateTime.UtcNow;
        var fileName =
            $"{prefix}-{utcNow:yyyyMMdd-HHmmss}.dump";
        var filePath = Path.Combine(settings.Path, fileName);

        var arguments = new[]
        {
            "-h", connection.Host,
            "-p", connection.Port.ToString(),
            "-d", connection.Database ?? string.Empty,
            "-U", connection.Username ?? string.Empty,
            "-Fc",
            "-f", filePath
        };
        var environment = new Dictionary<string, string>
        {
            ["PGPASSWORD"] = connection.Password ?? string.Empty
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds)));

        try
        {
            var process = await runner.RunAsync("pg_dump", arguments, environment, timeout.Token);

            if (process.ExitCode != 0)
            {
                DeleteIfExists(filePath);
                var error = string.IsNullOrWhiteSpace(process.StandardError)
                    ? $"pg_dump terminou com código {process.ExitCode}."
                    : process.StandardError.Trim();
                logger.LogWarning("Backup failed: {Error}", error);
                return new BackupResult(false, null, null, 0, error);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            DeleteIfExists(filePath);
            var error =
                $"Backup cancelado: tempo esgotado após {settings.TimeoutSeconds}s.";
            logger.LogWarning("Backup failed: {Error}", error);
            return new BackupResult(false, null, null, 0, error);
        }

        var info = new FileInfo(filePath);
        var status = new
        {
            File = fileName,
            CompletedAtUtc = utcNow.ToString("O"),
            DurationMs = (long)(DateTime.UtcNow - utcNow).TotalMilliseconds,
            SizeBytes = info.Length
        };
        await File.WriteAllTextAsync(
            Path.Combine(settings.Path, statusFileName),
            JsonSerializer.Serialize(status),
            cancellationToken);

        var pruned = PruneOldBackups(settings, prefix, utcNow);

        logger.LogInformation(
            "Backup completed: {File} ({SizeBytes} bytes), pruned {Pruned}.",
            fileName, info.Length, pruned);

        return new BackupResult(true, fileName, info.Length, pruned, null);
    }

    private int PruneOldBackups(BackupOptions settings, string prefix, DateTime utcNow)
    {
        var cutoff = utcNow.AddDays(-Math.Max(1, settings.RetentionDays));
        var pruned = 0;

        foreach (var file in Directory.EnumerateFiles(
                     settings.Path, $"{prefix}-*.dump"))
        {
            if (File.GetLastWriteTimeUtc(file) >= cutoff)
                continue;

            try
            {
                File.Delete(file);
                pruned++;
            }
            catch (IOException exception)
            {
                logger.LogWarning(
                    "Could not prune old backup {File}: {Reason}", file, exception.Message);
            }
        }

        return pruned;
    }

    private static void DeleteIfExists(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch (IOException)
        {
            // arquivo parcial remanescente não invalida o próximo dump
        }
    }
}

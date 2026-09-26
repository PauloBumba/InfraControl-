using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using InfraControl.Workers.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace InfraControl.IntegrationTests.Backup;

/// <summary>
/// F12-T004: restore validado — round-trip real no PostgreSQL do
/// Testcontainers: marker → pg_dump -Fc (PostgresBackupService de
/// verdade) → CREATE DATABASE → pg_restore → marker + contagens iguais.
/// Roda a cada build, atendendo o "teste periódico" do
/// docs/operations/BACKUP-RESTORE.md. Exige pg_dump/pg_restore no PATH —
/// se faltar, o teste FALHA com instrução (nunca skip escondido).
/// </summary>
public sealed class RestoreRoundTripTests : BaseIntegrationTest
{
    private const string RestoreDatabase = "infracontrol_restore_test";
    private string? _dumpDirectory;

    public override async Task DisposeAsync()
    {
        if (_dumpDirectory is not null && Directory.Exists(_dumpDirectory))
            Directory.Delete(_dumpDirectory, recursive: true);

        await base.DisposeAsync();
    }

    [Fact]
    public async Task BackupAndRestore_ShouldRecoverMarkerRowAndSchema()
    {
        var runner = new SystemProcessRunner();
        await RequireToolAsync(runner, "pg_dump");
        await RequireToolAsync(runner, "pg_restore");

        var connectionString = PostgresConnectionString;
        var marker = $"restore-marker-{Guid.NewGuid():N}";

        int originalLocations;
        int originalTables;
        using (var scope = CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Locations.Add(Location.Create(marker));
            await dbContext.SaveChangesAsync();
            originalLocations = await dbContext.Locations.CountAsync();
            originalTables = await CountPublicTablesAsync(connectionString);
        }

        _dumpDirectory = Path.Combine(
            Path.GetTempPath(), $"infracontrol-restore-{Guid.NewGuid():N}");
        var backupService = new PostgresBackupService(
            runner,
            Options.Create(new BackupOptions
            {
                ConnectionString = connectionString,
                Path = _dumpDirectory,
                TimeoutSeconds = 300
            }),
            NullLogger<PostgresBackupService>.Instance);

        var dump = await backupService.RunOnceAsync(CancellationToken.None);
        dump.Success.Should().BeTrue("o dump deve funcionar: {Error}", dump.Error);
        var dumpFile = Path.Combine(_dumpDirectory, dump.FileName!);
        File.Exists(dumpFile).Should().BeTrue();
        new FileInfo(dumpFile).Length.Should().BeGreaterThan(0);

        await using (var admin = new NpgsqlConnection(
                         WithDatabase(connectionString, "postgres")))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand(
                $"CREATE DATABASE {RestoreDatabase}", admin);
            await create.ExecuteNonQueryAsync();
        }

        var restoreBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var restore = await runner.RunAsync(
            "pg_restore",
            [
                "-h", restoreBuilder.Host,
                "-p", restoreBuilder.Port.ToString(),
                "-U", restoreBuilder.Username ?? string.Empty,
                "-d", RestoreDatabase,
                "--no-owner",
                dumpFile
            ],
            new Dictionary<string, string>
            {
                ["PGPASSWORD"] = restoreBuilder.Password ?? string.Empty
            },
            CancellationToken.None);

        restore.ExitCode.Should().Be(0,
            "o restore deve funcionar: {Error}", restore.StandardError);

        var restoredConnectionString = WithDatabase(connectionString, RestoreDatabase);
        await using var restored = new NpgsqlConnection(restoredConnectionString);
        await restored.OpenAsync();

        await using (var markerQuery = new NpgsqlCommand(
                         "SELECT count(*) FROM locations WHERE name = @name", restored))
        {
            markerQuery.Parameters.AddWithValue("name", marker);
            var markerCount = (long)(await markerQuery.ExecuteScalarAsync());
            markerCount.Should().Be(1, "a linha marker deve sobreviver ao round-trip");
        }

        await using (var locationsQuery = new NpgsqlCommand(
                         "SELECT count(*) FROM locations", restored))
        {
            var restoredLocations = (long)(await locationsQuery.ExecuteScalarAsync());
            restoredLocations.Should().Be(originalLocations);
        }

        (await CountPublicTablesAsync(restoredConnectionString))
            .Should().Be(originalTables, "o schema público deve ser idêntico");
    }

    private static async Task RequireToolAsync(IProcessRunner runner, string tool)
    {
        var probe = await runner.RunAsync(
            tool, ["--version"], new Dictionary<string, string>(), CancellationToken.None);

        probe.ExitCode.Should().Be(0,
            "o binário '{0}' precisa estar no PATH (ex.: instale postgresql-client) — " +
            "sem ele o round-trip de backup/restore não pode ser validado", tool);
    }

    private static async Task<int> CountPublicTablesAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'",
            connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static string WithDatabase(string connectionString, string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = database
        };
        return builder.ConnectionString;
    }
}

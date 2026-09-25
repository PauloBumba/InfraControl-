using FluentAssertions;
using InfraControl.Workers.Backup;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.Workers.Backup;

/// <summary>
/// F12-T003: PostgresBackupService — pg_dump -Fc com senha via ambiente
/// (nunca em argv), status latest.json, retenção de arquivos antigos e
/// falha/tempo esgotado sem deixar arquivo parcial (docs/operations/
/// BACKUP-RESTORE.md).
/// </summary>
public sealed class PostgresBackupServiceTests : IDisposable
{
    private readonly string _directory;
    private readonly string _connectionString =
        "Host=localhost;Port=5433;Database=infracontrol;Username=svc_backup;Password=SenhaDev!1";

    public PostgresBackupServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"infracontrol-backup-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private BackupOptions BuildOptions(Action<BackupOptions>? configure = null)
    {
        var options = new BackupOptions
        {
            ConnectionString = _connectionString,
            Path = _directory
        };
        configure?.Invoke(options);
        return options;
    }

    private PostgresBackupService Service(
        IProcessRunner runner, BackupOptions? options = null)
        => new(runner, Options.Create(options ?? BuildOptions()),
            NullLogger<PostgresBackupService>.Instance);

    [Fact]
    public void BackupOptions_ShouldHaveDefaults()
    {
        var options = new BackupOptions();

        options.Enabled.Should().BeTrue();
        options.IntervalHours.Should().Be(24);
        options.RetentionDays.Should().Be(7);
        options.Path.Should().Be("backups");
        options.FileNamePrefix.Should().Be("infracontrol");
        options.TimeoutSeconds.Should().Be(3600);
    }

    [Fact]
    public async Task RunOnce_WhenProcessSucceeds_ShouldCreateDumpWriteStatusAndPrune()
    {
        Directory.CreateDirectory(_directory);
        var oldFile = Path.Combine(_directory, "infracontrol-20200101-000000.dump");
        await File.WriteAllTextAsync(oldFile, "velho");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-10));
        var runner = new FakeProcessRunner { OnRun = args => WriteDumpFile(args) };

        var result = await Service(runner).RunOnceAsync(CancellationToken.None);

        result.Success.Should().BeTrue();
        result.FileName.Should().StartWith("infracontrol-").And.EndWith(".dump");
        result.SizeBytes.Should().BeGreaterThan(0);
        result.Pruned.Should().Be(1);
        File.Exists(oldFile).Should().BeFalse();

        var status = Path.Combine(_directory, "latest.json");
        File.Exists(status).Should().BeTrue();
        (await File.ReadAllTextAsync(status))
            .Should().Contain(result.FileName!);
    }

    [Fact]
    public async Task RunOnce_WhenProcessFails_ShouldDeletePartialFileAndReturnError()
    {
        var runner = new FakeProcessRunner
        {
            ExitCode = 1,
            StandardError = "pg_dump: error: connection to server was lost",
            OnRun = args => WriteDumpFile(args)
        };

        var result = await Service(runner).RunOnceAsync(CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("connection to server was lost");
        Directory.EnumerateFiles(_directory, "*.dump").Should().BeEmpty();
        File.Exists(Path.Combine(_directory, "latest.json")).Should().BeFalse();
    }

    [Fact]
    public async Task RunOnce_ShouldKeepPasswordOutOfArgumentsAndUseEnvironment()
    {
        var runner = new FakeProcessRunner { OnRun = args => WriteDumpFile(args) };

        await Service(runner).RunOnceAsync(CancellationToken.None);

        runner.LastArguments.Should().NotContain(arg =>
            arg.Contains("SenhaDev!1", StringComparison.Ordinal));
        runner.LastArguments.Should().Contain("-Fc");
        runner.LastEnvironment.Should().ContainKey("PGPASSWORD");
        runner.LastEnvironment!["PGPASSWORD"].Should().Be("SenhaDev!1");
        runner.LastArguments.Should().Contain("-h").And.Contain("localhost");
        runner.LastArguments.Should().Contain("-p").And.Contain("5433");
        runner.LastArguments.Should().Contain("-d").And.Contain("infracontrol");
        runner.LastArguments.Should().Contain("-U").And.Contain("svc_backup");
    }

    [Fact]
    public async Task RunOnce_WhenTimeoutElapses_ShouldFailWithoutPartialFile()
    {
        var runner = new FakeProcessRunner { WaitForCancellation = true };
        var options = BuildOptions(o => o.TimeoutSeconds = 1);

        var result = await Service(runner, options).RunOnceAsync(CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("tempo esgotado");
    }

    private static void WriteDumpFile(IReadOnlyList<string> arguments)
    {
        var fileIndex = Array.IndexOf(arguments.ToArray(), "-f");
        File.WriteAllText(arguments[fileIndex + 1], "-- dump fictício");
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public int ExitCode { get; init; }
        public string StandardError { get; init; } = string.Empty;
        public bool WaitForCancellation { get; init; }
        public Action<IReadOnlyList<string>>? OnRun { get; init; }
        public IReadOnlyList<string>? LastArguments { get; private set; }
        public IReadOnlyDictionary<string, string>? LastEnvironment { get; private set; }

        public async Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string> environment,
            CancellationToken cancellationToken)
        {
            LastArguments = arguments;
            LastEnvironment = environment;

            if (WaitForCancellation)
                await Task.Delay(Timeout.Infinite, cancellationToken);

            OnRun?.Invoke(arguments);
            return new ProcessResult(ExitCode, StandardError);
        }
    }
}

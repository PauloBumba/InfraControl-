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
        options.AdditionalTargets.Should().BeEmpty();
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
    public async Task RunOnce_WithAdditionalTarget_ShouldDumpBothTargetsWithOwnPrefixAndStatus()
    {
        Directory.CreateDirectory(_directory);
        var guacamoleConnection =
            "Host=guacamole-db;Port=5432;Database=guacamole;Username=guacamole;Password=GuacSenha!1";
        var options = BuildOptions(o => o.AdditionalTargets.Add(
            new BackupTargetOptions
            {
                ConnectionString = guacamoleConnection,
                FileNamePrefix = "guacamole"
            }));
        var runner = new FakeProcessRunner { OnRun = args => WriteDumpFile(args) };

        var result = await Service(runner, options).RunOnceAsync(CancellationToken.None);

        result.Success.Should().BeTrue();
        result.FileName.Should().StartWith("infracontrol-").And.EndWith(".dump");

        // Dois dumps, prefixos próprios, retenção por prefixo intacta.
        Directory.EnumerateFiles(_directory, "infracontrol-*.dump")
            .Should().HaveCount(1);
        Directory.EnumerateFiles(_directory, "guacamole-*.dump")
            .Should().HaveCount(1);

        // Heartbeat principal preservado + heartbeat do alvo adicional.
        File.Exists(Path.Combine(_directory, "latest.json")).Should().BeTrue();
        var guacStatus = Path.Combine(_directory, "latest-guacamole.json");
        File.Exists(guacStatus).Should().BeTrue();
        (await File.ReadAllTextAsync(guacStatus))
            .Should().Contain("guacamole-");

        // Senha de cada alvo só no ambiente da própria chamada (nunca em argv).
        runner.ArgumentCalls.Should().HaveCount(2);
        runner.EnvironmentCalls[0]!["PGPASSWORD"].Should().Be("SenhaDev!1");
        runner.EnvironmentCalls[1]!["PGPASSWORD"].Should().Be("GuacSenha!1");
        runner.ArgumentCalls.Should().OnlyContain(args =>
            !args.Any(arg => arg.Contains("SenhaDev!1", StringComparison.Ordinal) ||
                             arg.Contains("GuacSenha!1", StringComparison.Ordinal)));
        runner.ArgumentCalls[1].Should().Contain("guacamole-db").And.Contain("guacamole");
    }

    [Fact]
    public async Task RunOnce_WhenAdditionalTargetFails_ShouldFailButKeepPrimaryDump()
    {
        Directory.CreateDirectory(_directory);
        var options = BuildOptions(o => o.AdditionalTargets.Add(
            new BackupTargetOptions
            {
                ConnectionString = "Host=guacamole-db;Port=5432;Database=guacamole;Username=guacamole;Password=x",
                FileNamePrefix = "guacamole"
            }));
        var runner = new FakeProcessRunner
        {
            OnRunIndexed = (index, args) =>
            {
                if (index == 1)
                    return new ProcessResult(1, "pg_dump: guacamole-db recusou a conexão");
                WriteDumpFile(args);
                return new ProcessResult(0, string.Empty);
            }
        };

        var result = await Service(runner, options).RunOnceAsync(CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("guacamole-db recusou a conexão");

        // O dump principal sobrevive à falha do adicional (alvos independentes).
        Directory.EnumerateFiles(_directory, "infracontrol-*.dump")
            .Should().HaveCount(1);
        File.Exists(Path.Combine(_directory, "latest.json")).Should().BeTrue();

        // Do adicional falho: nem dump parcial nem heartbeat.
        Directory.EnumerateFiles(_directory, "guacamole-*.dump").Should().BeEmpty();
        File.Exists(Path.Combine(_directory, "latest-guacamole.json")).Should().BeFalse();
    }

    [Fact]
    public async Task RunOnce_WhenAdditionalTargetHasEmptyConnectionString_ShouldSkipIt()
    {
        var options = BuildOptions(o => o.AdditionalTargets.Add(
            new BackupTargetOptions { ConnectionString = " ", FileNamePrefix = "vazio" }));
        var runner = new FakeProcessRunner { OnRun = args => WriteDumpFile(args) };

        var result = await Service(runner, options).RunOnceAsync(CancellationToken.None);

        result.Success.Should().BeTrue();
        runner.ArgumentCalls.Should().HaveCount(1);
        Directory.EnumerateFiles(_directory, "vazio-*.dump").Should().BeEmpty();
    }

    [Fact]
    public async Task RunOnce_WithAdditionalTarget_ShouldPruneEachPrefixIndependently()
    {
        Directory.CreateDirectory(_directory);
        var oldMain = Path.Combine(_directory, "infracontrol-20200101-000000.dump");
        var oldGuac = Path.Combine(_directory, "guacamole-20200101-000000.dump");
        await File.WriteAllTextAsync(oldMain, "velho");
        await File.WriteAllTextAsync(oldGuac, "velho");
        File.SetLastWriteTimeUtc(oldMain, DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(oldGuac, DateTime.UtcNow.AddDays(-10));
        var options = BuildOptions(o => o.AdditionalTargets.Add(
            new BackupTargetOptions
            {
                ConnectionString = "Host=guacamole-db;Database=guacamole;Username=guacamole;Password=x",
                FileNamePrefix = "guacamole"
            }));
        var runner = new FakeProcessRunner { OnRun = args => WriteDumpFile(args) };

        var result = await Service(runner, options).RunOnceAsync(CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Pruned.Should().Be(2);
        File.Exists(oldMain).Should().BeFalse();
        File.Exists(oldGuac).Should().BeFalse();
        Directory.EnumerateFiles(_directory, "*.dump")
            .Should().HaveCount(2); // um novo por prefixo
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
        public Func<int, IReadOnlyList<string>, ProcessResult>? OnRunIndexed { get; init; }
        public List<IReadOnlyList<string>> ArgumentCalls { get; } = [];
        public List<IReadOnlyDictionary<string, string>?> EnvironmentCalls { get; } = [];
        public IReadOnlyList<string>? LastArguments => ArgumentCalls.Count == 0 ? null : ArgumentCalls[^1];
        public IReadOnlyDictionary<string, string>? LastEnvironment =>
            EnvironmentCalls.Count == 0 ? null : EnvironmentCalls[^1];

        public async Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string> environment,
            CancellationToken cancellationToken)
        {
            ArgumentCalls.Add(arguments);
            EnvironmentCalls.Add(environment);

            if (WaitForCancellation)
                await Task.Delay(Timeout.Infinite, cancellationToken);

            if (OnRunIndexed is not null)
                return OnRunIndexed(ArgumentCalls.Count - 1, arguments);

            OnRun?.Invoke(arguments);
            return new ProcessResult(ExitCode, StandardError);
        }
    }
}

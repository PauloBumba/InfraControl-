using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Operations.Backup.Ports;
using InfraControl.Application.Operations.Retention;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.Operations;

/// <summary>
/// Tela de retenção completa (F50): linhas novas Grafana/Zabbix/Databases
/// (política gerenciada — registrada, não aplicada pelo cleanup) e Backups
/// (mesmo valor do BackupSettings.RetentionDays — as duas telas editam o
/// mesmo número) + export/import JSON.
/// </summary>
public sealed class RetentionExtendedTypesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public RetentionExtendedTypesTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static UpsertRetentionPolicyCommandHandler UpsertHandler(AppDbContext context)
        => new(new RetentionPolicyRepository(context), new BackupSettingsRepository(context));

    private static DeleteRetentionPolicyCommandHandler DeleteHandler(AppDbContext context)
        => new(new RetentionPolicyRepository(context), new BackupSettingsRepository(context));

    private static GetRetentionPoliciesQueryHandler QueryHandler(AppDbContext context)
        => new(
            new RetentionPolicyRepository(context),
            Options.Create(new RetentionOptions()),
            new BackupSettingsRepository(context));

    private static ExportRetentionPoliciesQueryHandler ExportHandler(AppDbContext context)
        => new(new GetRetentionPoliciesQueryHandler(
            new RetentionPolicyRepository(context),
            Options.Create(new RetentionOptions()),
            new BackupSettingsRepository(context)));

    private static ImportRetentionPoliciesCommandHandler ImportHandler(AppDbContext context)
        => new(new RetentionPolicyRepository(context), new BackupSettingsRepository(context));

    [Fact]
    public async Task Query_IncludesManagedAndBackupTypes()
    {
        await using var context = await CreateContextAsync();

        var dtos = await QueryHandler(context).Handle(
            new GetRetentionPoliciesQuery(), CancellationToken.None);

        dtos.Should().HaveCount(14);
        dtos.Single(d => d.DataType == "Grafana")
            .Should().BeEquivalentTo(
                new RetentionPolicyDto("Grafana", 30, "Config"));
        dtos.Single(d => d.DataType == "Zabbix").Days.Should().Be(90);
        dtos.Single(d => d.DataType == "Databases").Days.Should().Be(365);
        dtos.Single(d => d.DataType == "Backups")
            .Should().BeEquivalentTo(
                new RetentionPolicyDto("Backups", 7, "BackupSettings"));
    }

    [Fact]
    public async Task Upsert_Backups_WritesBackupSettings_NotPolicyRow()
    {
        await using var context = await CreateContextAsync();

        var dto = await UpsertHandler(context).Handle(
            new UpsertRetentionPolicyCommand(RetentionDataType.Backups, 30),
            CancellationToken.None);

        dto.Should().BeEquivalentTo(
            new RetentionPolicyDto("Backups", 30, "BackupSettings"));
        (await context.RetentionPolicies.CountAsync()).Should().Be(0);
        var settings = await new BackupSettingsRepository(context)
            .GetAsync(CancellationToken.None);
        settings!.RetentionDays.Should().Be(30);
    }

    [Fact]
    public async Task Delete_Backups_ResetsToDefaultSeven()
    {
        await using var context = await CreateContextAsync();
        await UpsertHandler(context).Handle(
            new UpsertRetentionPolicyCommand(RetentionDataType.Backups, 30),
            CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteRetentionPolicyCommand(RetentionDataType.Backups),
            CancellationToken.None);

        var settings = await new BackupSettingsRepository(context)
            .GetAsync(CancellationToken.None);
        settings!.RetentionDays.Should().Be(7);
    }

    [Fact]
    public async Task Upsert_ManagedType_PersistsPolicyRow()
    {
        await using var context = await CreateContextAsync();

        var dto = await UpsertHandler(context).Handle(
            new UpsertRetentionPolicyCommand(RetentionDataType.Zabbix, 60),
            CancellationToken.None);

        dto.Should().BeEquivalentTo(
            new RetentionPolicyDto("Zabbix", 60, "Policy"));
        (await context.RetentionPolicies.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Export_ReturnsAllTypesWithEffectiveDays()
    {
        await using var context = await CreateContextAsync();
        await UpsertHandler(context).Handle(
            new UpsertRetentionPolicyCommand(RetentionDataType.AuditEntries, 45),
            CancellationToken.None);
        await UpsertHandler(context).Handle(
            new UpsertRetentionPolicyCommand(RetentionDataType.Backups, 20),
            CancellationToken.None);

        var export = await ExportHandler(context).Handle(
            new ExportRetentionPoliciesQuery(), CancellationToken.None);

        export.Policies.Should().HaveCount(14);
        export.Policies.Single(p => p.DataType == "AuditEntries").Days.Should().Be(45);
        export.Policies.Single(p => p.DataType == "Backups").Days.Should().Be(20);
    }

    [Fact]
    public async Task Import_AppliesValidatedPolicies()
    {
        await using var context = await CreateContextAsync();

        var result = await ImportHandler(context).Handle(
            new ImportRetentionPoliciesCommand(
            [
                new RetentionExportItem("AuditEntries", 60),
                new RetentionExportItem("Backups", 15),
                new RetentionExportItem("Zabbix", 45),
            ]),
            CancellationToken.None);

        result.Imported.Should().Be(3);
        var audit = await new RetentionPolicyRepository(context)
            .GetByTypeAsync(RetentionDataType.AuditEntries, CancellationToken.None);
        audit!.Days.Should().Be(60);
        var settings = await new BackupSettingsRepository(context)
            .GetAsync(CancellationToken.None);
        settings!.RetentionDays.Should().Be(15);
    }

    [Fact]
    public async Task Import_UnknownType_ThrowsPtBr()
    {
        await using var context = await CreateContextAsync();

        var act = () => ImportHandler(context).Handle(
            new ImportRetentionPoliciesCommand(
                [new RetentionExportItem("tipoX", 30)]),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*tipoX*");
    }

    [Fact]
    public async Task Import_DaysOutOfRange_ThrowsPtBr()
    {
        await using var context = await CreateContextAsync();

        var act = () => ImportHandler(context).Handle(
            new ImportRetentionPoliciesCommand(
                [new RetentionExportItem("AuditEntries", 0)]),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*entre 1 e 3650*");
    }
}

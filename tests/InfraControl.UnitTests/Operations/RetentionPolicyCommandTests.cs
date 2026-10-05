using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
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
/// F12-T002: gestão de RetentionPolicy — upsert/reset com validação,
/// RequiredRoles Administer (RetentionManage) e GET com valor efetivo
/// (policy sobrepõe o default de Retention:* — docs/11).
/// </summary>
public sealed class RetentionPolicyCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public RetentionPolicyCommandTests()
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
        => new(new RetentionPolicyRepository(context));

    private static DeleteRetentionPolicyCommandHandler DeleteHandler(AppDbContext context)
        => new(new RetentionPolicyRepository(context));

    private static GetRetentionPoliciesQueryHandler QueryHandler(AppDbContext context)
        => new(new RetentionPolicyRepository(context), Options.Create(new RetentionOptions()));

    [Fact]
    public async Task Upsert_NewPolicy_ShouldPersist()
    {
        await using var context = await CreateContextAsync();

        var dto = await UpsertHandler(context).Handle(
            new UpsertRetentionPolicyCommand(RetentionDataType.AuditEntries, 45),
            CancellationToken.None);

        dto.DataType.Should().Be("AuditEntries");
        dto.Days.Should().Be(45);
        dto.Source.Should().Be("Policy");
        (await context.RetentionPolicies.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Upsert_ExistingPolicy_ShouldUpdateDaysWithoutDuplicating()
    {
        await using var context = await CreateContextAsync();
        await UpsertHandler(context).Handle(
            new UpsertRetentionPolicyCommand(RetentionDataType.AuditEntries, 45),
            CancellationToken.None);

        var updated = await UpsertHandler(context).Handle(
            new UpsertRetentionPolicyCommand(RetentionDataType.AuditEntries, 30),
            CancellationToken.None);

        updated.Days.Should().Be(30);
        (await context.RetentionPolicies.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3651)]
    public async Task Upsert_WithDaysOutOfRange_ShouldThrowPtBr(int days)
    {
        await using var context = await CreateContextAsync();

        var action = () => UpsertHandler(context).Handle(
            new UpsertRetentionPolicyCommand(RetentionDataType.Notifications, days),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*entre 1 e 3650*");
        (await context.RetentionPolicies.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Delete_ExistingPolicy_ShouldRemove_ThenSecondDeleteIs404()
    {
        await using var context = await CreateContextAsync();
        await UpsertHandler(context).Handle(
            new UpsertRetentionPolicyCommand(RetentionDataType.RemoteSessions, 7),
            CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteRetentionPolicyCommand(RetentionDataType.RemoteSessions),
            CancellationToken.None);
        (await context.RetentionPolicies.CountAsync()).Should().Be(0);

        var action = () => DeleteHandler(context).Handle(
            new DeleteRetentionPolicyCommand(RetentionDataType.RemoteSessions),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("*não encontrada*");
    }

    [Fact]
    public async Task GetPolicies_WithoutPolicies_ShouldReturnAllNineTypesWithConfigDefaults()
    {
        await using var context = await CreateContextAsync();

        var policies = await QueryHandler(context).Handle(
            new GetRetentionPoliciesQuery(), CancellationToken.None);

        policies.Should().HaveCount(9);
        policies.Should().OnlyContain(p => p.Source == "Config");
        policies.Single(p => p.DataType == "OutboxMessages").Days.Should().Be(30);
        policies.Single(p => p.DataType == "ProcessedMessages").Days.Should().Be(30);
        policies.Single(p => p.DataType == "AuditEntries").Days.Should().Be(90);
        policies.Single(p => p.DataType == "MonitoringResults").Days.Should().Be(180);
        policies.Single(p => p.DataType == "Notifications").Days.Should().Be(90);
        policies.Single(p => p.DataType == "RemoteSessions").Days.Should().Be(90);
        policies.Single(p => p.DataType == "DiscoveryRuns").Days.Should().Be(180);
        policies.Single(p => p.DataType == "MonitoringEvents").Days.Should().Be(90);
        policies.Single(p => p.DataType == "Invitations").Days.Should().Be(90);
    }

    [Fact]
    public async Task GetPolicies_WithPolicy_ShouldUsePolicyOverConfigAndFilterByType()
    {
        await using var context = await CreateContextAsync();
        await UpsertHandler(context).Handle(
            new UpsertRetentionPolicyCommand(RetentionDataType.RemoteSessions, 7),
            CancellationToken.None);

        var all = await QueryHandler(context).Handle(
            new GetRetentionPoliciesQuery(), CancellationToken.None);
        var single = await QueryHandler(context).Handle(
            new GetRetentionPoliciesQuery(RetentionDataType.RemoteSessions),
            CancellationToken.None);

        all.Single(p => p.DataType == "RemoteSessions")
            .Should().BeEquivalentTo(new RetentionPolicyDto("RemoteSessions", 7, "Policy"));
        all.Single(p => p.DataType == "AuditEntries").Source.Should().Be("Config");
        single.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new RetentionPolicyDto("RemoteSessions", 7, "Policy"));
    }

    [Fact]
    public void Requests_ShouldDeclareAdminRoleAndAuditResource()
    {
        var required = RoleAccess.Administer;

        new UpsertRetentionPolicyCommand(RetentionDataType.AuditEntries, 30)
            .RequiredRoles.Should().BeEquivalentTo(required);
        new DeleteRetentionPolicyCommand(RetentionDataType.AuditEntries)
            .RequiredRoles.Should().BeEquivalentTo(required);
        new GetRetentionPoliciesQuery()
            .RequiredRoles.Should().BeEquivalentTo(required);

        ((IAuditableRequest)new UpsertRetentionPolicyCommand(
            RetentionDataType.AuditEntries, 30)).AuditResource.Should().Be("RetentionPolicy");
        ((IAuditableRequest)new DeleteRetentionPolicyCommand(
            RetentionDataType.AuditEntries)).AuditResource.Should().Be("RetentionPolicy");
        ((IAuditableRequest)new GetRetentionPoliciesQuery())
            .AuditResource.Should().Be("RetentionPolicy");
    }
}

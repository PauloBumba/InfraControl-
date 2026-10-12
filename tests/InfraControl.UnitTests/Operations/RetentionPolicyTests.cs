using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Operations;

/// <summary>
/// F12-T002: RetentionPolicy — política de retenção por tipo de dado com
/// validação de dias em pt-BR (docs/03, docs/11).
/// </summary>
public sealed class RetentionPolicyTests
{
    [Fact]
    public void Create_WithValidDays_ShouldPersistDataTypeAndDays()
    {
        var policy = RetentionPolicy.Create(RetentionDataType.AuditEntries, 45);

        policy.Id.Should().NotBeEmpty();
        policy.DataType.Should().Be(RetentionDataType.AuditEntries);
        policy.Days.Should().Be(45);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3651)]
    public void Create_WithDaysOutOfRange_ShouldThrowPtBr(int days)
    {
        var action = () => RetentionPolicy.Create(RetentionDataType.Notifications, days);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*entre 1 e 3650*");
    }

    [Fact]
    public void Create_WithUnknownDataType_ShouldThrow()
    {
        var action = () => RetentionPolicy.Create((RetentionDataType)99, 30);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Update_WithValidDays_ShouldChangeDays()
    {
        var policy = RetentionPolicy.Create(RetentionDataType.RemoteSessions, 90);

        policy.Update(30);

        policy.Days.Should().Be(30);
        policy.DataType.Should().Be(RetentionDataType.RemoteSessions);
    }

    [Fact]
    public void Update_WithDaysOutOfRange_ShouldThrowPtBr()
    {
        var policy = RetentionPolicy.Create(RetentionDataType.MonitoringResults, 180);

        var action = () => policy.Update(0);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*entre 1 e 3650*");
        policy.Days.Should().Be(180);
    }

    [Fact]
    public void DataType_ShouldCoverExactlyTheTenCleanupTypes()
    {
        Enum.GetValues<RetentionDataType>().Should().BeEquivalentTo([
            RetentionDataType.OutboxMessages,
            RetentionDataType.ProcessedMessages,
            RetentionDataType.AuditEntries,
            RetentionDataType.MonitoringResults,
            RetentionDataType.Notifications,
            RetentionDataType.RemoteSessions,
            RetentionDataType.DiscoveryRuns,
            RetentionDataType.MonitoringEvents,
            RetentionDataType.Invitations,
            RetentionDataType.UniFiSyncRuns
        ]);
    }
}

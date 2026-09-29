using FluentAssertions;
using InfraControl.Application.Operations.Retention;
using InfraControl.Domain.Enums;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Operations;

/// <summary>
/// F12-T002: RetentionPolicy no PostgreSQL real — 7 tipos com default de
/// config, upsert/reset com autorização RetentionManage (Admin-only).
/// </summary>
public sealed class RetentionPolicyIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task GetPolicies_WithAdminRole_ShouldReturnSevenTypesWithConfigDefaults()
    {
        using var scope = CreateScope();
        UseRole(UserRole.Admin);
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var policies = await mediator.Send(new GetRetentionPoliciesQuery());

        policies.Should().HaveCount(7);
        policies.Should().OnlyContain(p => p.Source == "Config");
        policies.Single(p => p.DataType == "AuditEntries").Days.Should().Be(90);
        policies.Single(p => p.DataType == "RemoteSessions").Days.Should().Be(90);
    }

    [Fact]
    public async Task PutAndDelete_WithAdminRole_ShouldSwitchSourcePolicyThenBackToConfig()
    {
        using var scope = CreateScope();
        UseRole(UserRole.Admin);
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(new UpsertRetentionPolicyCommand(
            RetentionDataType.RemoteSessions, 7));
        var withPolicy = await mediator.Send(
            new GetRetentionPoliciesQuery(RetentionDataType.RemoteSessions));

        withPolicy.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new RetentionPolicyDto("RemoteSessions", 7, "Policy"));

        await mediator.Send(new DeleteRetentionPolicyCommand(
            RetentionDataType.RemoteSessions));
        var afterDelete = await mediator.Send(
            new GetRetentionPoliciesQuery(RetentionDataType.RemoteSessions));

        afterDelete.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new RetentionPolicyDto("RemoteSessions", 90, "Config"));
    }

    [Fact]
    public async Task Put_WithInvalidDays_ShouldThrowArgumentOutOfRangePtBr()
    {
        using var scope = CreateScope();
        UseRole(UserRole.Admin);
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var action = () => mediator.Send(new UpsertRetentionPolicyCommand(
            RetentionDataType.Notifications, 0));

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*entre 1 e 3650*");
    }

    [Fact]
    public async Task Put_WithUserRole_ShouldThrowUnauthorizedRetentionManage()
    {
        using var scope = CreateScope();
        UseRole(UserRole.Operator);
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var action = () => mediator.Send(new UpsertRetentionPolicyCommand(
            RetentionDataType.AuditEntries, 30));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin");
    }

    [Fact]
    public async Task Get_WithViewerRole_ShouldThrowUnauthorizedRetentionManage()
    {
        using var scope = CreateScope();
        UseRole(UserRole.Viewer);
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var action = () => mediator.Send(new GetRetentionPoliciesQuery());

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin");
    }

    [Fact]
    public async Task Delete_NonExistingPolicy_WithAdminRole_ShouldThrowKeyNotFound()
    {
        using var scope = CreateScope();
        UseRole(UserRole.Admin);
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var action = () => mediator.Send(new DeleteRetentionPolicyCommand(
            RetentionDataType.DiscoveryRuns));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("*não encontrada*");
    }
}

using FluentAssertions;
using InfraControl.Application.Discovery.Runs;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Discovery;

/// <summary>
/// F4-T003: gatilho manual de discovery no pipeline real — cria run,
/// sonda via seam ICMP falsa, grava achado e Complete no PostgreSQL do
/// Testcontainers. Sem rede real.
/// </summary>
public sealed class StartDiscoveryIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task Start_WithAdminRole_ShouldPersistRunAndResult()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.50");
        UseRole(UserRole.Admin);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dto = await mediator.Send(new StartDiscoveryCommand(device.Id, "Icmp"));

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));

        var run = dbContext.DiscoveryRuns.Single(r => r.Id == dto.Id);
        run.Status.Should().Be(DiscoveryRunStatus.Completed);
        var result = dbContext.DiscoveryResults.Single();
        result.DiscoveryRunId.Should().Be(run.Id);
        result.Kind.Should().Be(DiscoveryResultKind.Host);
        result.Values.Should().NotBeNull();
        result.Values!["reachable"].Should().Be("true");
    }

    [Fact]
    public async Task Start_WithViewerRole_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.51");
        UseRole(UserRole.Viewer);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var act = () => mediator.Send(new StartDiscoveryCommand(device.Id, "Icmp"));

        (await act.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*MonitoringManage*");
    }

    [Fact]
    public async Task Start_Snmp_WithUserRole_ShouldPersistSystemGroupResult()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.60");
        UseRole(UserRole.User);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dto = await mediator.Send(new StartDiscoveryCommand(device.Id, "Snmp"));

        dto.Status.Should().Be(nameof(DiscoveryRunStatus.Completed));
        var result = dbContext.DiscoveryResults.Single();
        result.Kind.Should().Be(DiscoveryResultKind.Host);
        result.Values!["sys_name"].Should().Be("sw-core-01");
    }

    [Fact]
    public async Task Start_UnknownProvider_ShouldThrow400PtBr()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.52");
        UseRole(UserRole.User);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var act = () => mediator.Send(new StartDiscoveryCommand(device.Id, "Foo"));

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*Provider de discovery*");
        dbContext.DiscoveryRuns.Should().BeEmpty();
    }
}

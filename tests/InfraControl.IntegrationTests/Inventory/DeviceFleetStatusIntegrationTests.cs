using FluentAssertions;
using InfraControl.Application.Inventory.Devices.Queries;
using InfraControl.Application.Monitoring.Checks;
using InfraControl.Application.Monitoring.Results;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Inventory;

/// <summary>
/// F54-T001: overview agregado da frota e série de disponibilidade —
/// validam no PostgreSQL real os GROUP BY novos dos repositórios
/// (alertas Open por device/severidade; células de 5 min de polls).
/// </summary>
public sealed class DeviceFleetStatusIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task FleetStatus_ShouldAggregateAlertsAndMonitoringSignals()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var healthy = await SeedDeviceAsync(dbContext, ip: "192.0.2.40", name: "sw-ok");
        var silent = await SeedDeviceAsync(dbContext, ip: null, name: "sw-silent");
        var critical = await SeedDeviceAsync(dbContext, ip: "192.0.2.41", name: "sw-alert");

        dbContext.Alerts.Add(Alert.Create(
            critical.Id, CheckKind.Ping, AlertSeverity.Critical, "host sem resposta"));
        await dbContext.SaveChangesAsync();

        UseRole(UserRole.Admin);
        await mediator.Send(new RunCheckCommand(healthy.Id, "Ping"));

        var result = await mediator.Send(new GetDeviceFleetStatusQuery());

        result.TotalDevices.Should().Be(3);
        result.ByType.Should().Contain(t => t.Type == "Switch" && t.Count == 3);
        result.OpenAlerts.Should().Be(1);
        result.OpenCriticalAlerts.Should().Be(1);
        result.OpenWarningAlerts.Should().Be(0);
        // critical → Unavailable; healthy → Available (poll recente ok);
        // silent (sem IP, sem checks) → Unknown.
        result.ByStatus.Should().Contain(s => s.Status == "Unavailable" && s.Count == 1);
        result.ByStatus.Should().Contain(s => s.Status == "Available" && s.Count == 1);
        result.ByStatus.Should().Contain(s => s.Status == "Unknown" && s.Count == 1);
    }

    [Fact]
    public async Task Availability_ShouldBucketPollsIntoRequestedIntervals()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.42", name: "sw-avail");

        UseRole(UserRole.Admin);
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var run = await mediator.Send(new RunCheckCommand(device.Id, "Ping"));
        run.Succeeded.Should().BeTrue();

        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddHours(-1);
        var result = await mediator.Send(
            new GetMonitoringAvailabilityQuery(fromUtc, toUtc, 15));

        result.Points.Should().NotBeEmpty();
        result.Points.Sum(p => p.Total).Should().BeGreaterThanOrEqualTo(1);
        result.Points.Sum(p => p.Succeeded).Should().BeGreaterThanOrEqualTo(1);
        result.Points.Sum(p => p.Failed).Should().Be(0);
        result.Points.Should().OnlyContain(p => p.BucketStartUtc >= fromUtc);
    }
}

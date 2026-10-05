using FluentAssertions;
using InfraControl.Application.Monitoring.Checks;
using InfraControl.Application.Monitoring.Results;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Monitoring;

/// <summary>
/// MON-009 (docs/21): fluxo ponta-a-ponta do monitoring — RunCheckCommand
/// atravessa o pipeline real (AuthorizationBehavior por role do usuário),
/// grava no PostgreSQL do Testcontainers e é lido de volta pela query do
/// histórico. Nenhum teste usa rede real (ICMP falso).
/// </summary>
public sealed class MonitoringIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task RunCheck_WithOperatorRole_ShouldPersistHistoryAndReadBackViaQuery()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.10");
        UseRole(UserRole.Operator);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var run = await mediator.Send(new RunCheckCommand(device.Id, "Ping"));

        run.Executed.Should().BeTrue();
        run.Succeeded.Should().BeTrue();

        var history = await mediator.Send(new GetDeviceResultsQuery(device.Id));
        history.DeviceId.Should().Be(device.Id);
        history.Results.Should().ContainSingle(result =>
            result.Check == "Ping" && result.Succeeded);
    }

    /// <summary>
    /// ADR-030 §3: executar check é mutação (Operate) — Viewer lê o
    /// histórico, não dispara execução.
    /// </summary>
    [Fact]
    public async Task RunCheck_WithViewerRole_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.12");
        UseRole(UserRole.Viewer);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(new RunCheckCommand(device.Id, "Ping"));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator");
    }

    [Fact]
    public async Task RunCheck_WithoutRole_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.11");
        UseNoRoles();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(new RunCheckCommand(device.Id, "Ping"));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator");
    }

    [Fact]
    public async Task RunCheck_WithoutIp_ShouldNotPersistHistory()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: null);
        UseRole(UserRole.Admin);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var run = await mediator.Send(new RunCheckCommand(device.Id, "Ping"));

        run.Executed.Should().BeFalse();
        run.Error.Should().Be("Device sem IP cadastrado.");

        var history = await mediator.Send(new GetDeviceResultsQuery(device.Id));
        history.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAllChecks_WithMixedDevices_ShouldPersistOnlyDevicesWithIp()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var withIp = await SeedDeviceAsync(dbContext, ip: "192.0.2.20", name: "sw-up");
        var withoutIp = await SeedDeviceAsync(dbContext, ip: null, name: "sw-noip");
        UseRole(UserRole.Admin);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var results = await mediator.Send(new RunAllChecksCommand("Ping"));

        results.Should().HaveCount(2);
        results.Single(result => result.DeviceId == withIp.Id).Succeeded.Should().BeTrue();
        results.Single(result => result.DeviceId == withoutIp.Id).Executed.Should().BeFalse();

        var persisted = await mediator.Send(new GetDeviceResultsQuery(withIp.Id));
        persisted.Results.Should().ContainSingle();
        var skipped = await mediator.Send(new GetDeviceResultsQuery(withoutIp.Id));
        skipped.Results.Should().BeEmpty();
    }
}

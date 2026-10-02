using FluentAssertions;
using InfraControl.Application.Discovery.Clients.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Discovery;

/// <summary>
/// F18-T008: `GET /api/v1/clients` no pipeline real (AuthorizationBehavior
/// por role) sobre o PostgreSQL do Testcontainers — agregação de ARP/DHCP de
/// todas as execuções com dedupe por MAC.
/// </summary>
public sealed class ClientsIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task Search_WithViewerRole_ShouldAggregateAndDedupeAcrossRuns()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.31");
        await SeedTwoRunsWithSameMacAsync(dbContext, device.Id);
        UseRole(UserRole.Viewer);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var page = await mediator.Send(new SearchClientsQuery());

        page.TotalCount.Should().Be(1, "o mesmo MAC das duas execuções é 1 cliente");
        var client = page.Items.Single();
        client.Mac.Should().Be("aa:bb:cc:dd:ee:ff", "o MAC vem da linha mais nova, sem normalizar na saída");
        client.Ip.Should().Be("192.0.2.50");
        client.Hostname.Should().Be("pc-01");
        client.Kind.Should().Be(nameof(DiscoveryResultKind.DhcpLease));
        client.LastSeenAt.Should().Be(new DateTime(2026, 9, 28, 13, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Search_TermFilter_ShouldRoundTripOnPostgres()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.32");
        await SeedTwoRunsWithSameMacAsync(dbContext, device.Id);
        UseRole(UserRole.Viewer);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var porHostname = await mediator.Send(new SearchClientsQuery(Term: "PC-01"));
        var porIp = await mediator.Send(new SearchClientsQuery(Term: "192.0.2.50"));
        var nada = await mediator.Send(new SearchClientsQuery(Term: "inexistente"));

        porHostname.TotalCount.Should().Be(1, "o termo casa hostname sem diferenciar maiúsculas");
        porIp.TotalCount.Should().Be(1);
        nada.TotalCount.Should().Be(0);
        nada.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_WithoutRole_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        UseNoRoles();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var act = () => mediator.Send(new SearchClientsQuery());

        (await act.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator, Viewer");
    }

    private static async Task SeedTwoRunsWithSameMacAsync(AppDbContext dbContext, Guid deviceId)
    {
        var run1 = DiscoveryRun.Start(deviceId);
        run1.Complete();
        var run2 = DiscoveryRun.Start(deviceId);
        run2.Complete();
        dbContext.DiscoveryRuns.AddRange(run1, run2);

        dbContext.DiscoveryResults.Add(DiscoveryResult.Create(
            run1.Id,
            DiscoveryResultKind.ArpEntry,
            "192.0.2.50 ativo",
            new Dictionary<string, string>
            {
                ["ip"] = "192.0.2.50",
                ["mac"] = "AA:BB:CC:DD:EE:FF",
            },
            createdAt: new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc)));

        dbContext.DiscoveryResults.Add(DiscoveryResult.Create(
            run2.Id,
            DiscoveryResultKind.DhcpLease,
            "192.0.2.50 pc-01",
            new Dictionary<string, string>
            {
                ["ip"] = "192.0.2.50",
                ["mac"] = "aa:bb:cc:dd:ee:ff",
                ["hostname"] = "pc-01",
            },
            createdAt: new DateTime(2026, 9, 28, 13, 0, 0, DateTimeKind.Utc)));
        await dbContext.SaveChangesAsync();
    }
}

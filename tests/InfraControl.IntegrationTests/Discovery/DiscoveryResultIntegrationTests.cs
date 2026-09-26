using FluentAssertions;
using InfraControl.Application.Discovery.Results.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Discovery;

/// <summary>
/// F4-T002: leitura dos resultados de discovery no pipeline real
/// (AuthorizationBehavior por role) gravados no PostgreSQL do
/// Testcontainers. Sem rede — providers de escrita vêm em F4-T003+.
/// </summary>
public sealed class DiscoveryResultIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task ListAndGet_WithViewerRole_ShouldRoundTrip()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.30");
        var (runId, resultId) = await SeedRunWithResultAsync(dbContext, device.Id);
        UseRole(UserRole.Viewer);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var page = await mediator.Send(new SearchDiscoveryResultsQuery(runId));
        var item = await mediator.Send(new GetDiscoveryResultQuery(resultId));

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle(item => item.Id == resultId);
        item.Kind.Should().Be(nameof(DiscoveryResultKind.Neighbor));
        item.Summary.Should().Be("sw-core-01 via Gi0/1");
        item.Values.Should().NotBeNull();
        item.Values!["neighbor"].Should().Be("sw-core-01");
    }

    [Fact]
    public async Task List_WithoutMonitoringReadPermission_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        UseNoPermissions();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var act = () => mediator.Send(new SearchDiscoveryResultsQuery(Guid.NewGuid()));

        (await act.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*MonitoringRead*");
    }

    [Fact]
    public async Task List_UnknownRun_ShouldThrowNotFoundPtBr()
    {
        using var scope = CreateScope();
        UseRole(UserRole.Admin);
        var unknownId = Guid.NewGuid();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var act = () => mediator.Send(new SearchDiscoveryResultsQuery(unknownId));

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Execução de discovery '{unknownId}' não encontrada.");
    }

    private static async Task<(Guid RunId, Guid ResultId)> SeedRunWithResultAsync(
        AppDbContext dbContext,
        Guid deviceId)
    {
        var run = DiscoveryRun.Start(deviceId);
        run.Complete();
        dbContext.DiscoveryRuns.Add(run);

        var result = DiscoveryResult.Create(
            run.Id,
            DiscoveryResultKind.Neighbor,
            "sw-core-01 via Gi0/1",
            new Dictionary<string, string> { ["neighbor"] = "sw-core-01", ["local_port"] = "Gi0/1" });
        dbContext.DiscoveryResults.Add(result);
        await dbContext.SaveChangesAsync();
        return (run.Id, result.Id);
    }
}

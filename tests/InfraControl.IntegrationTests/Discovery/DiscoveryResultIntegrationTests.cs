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
    public async Task List_WithoutRole_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        UseNoRoles();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var act = () => mediator.Send(new SearchDiscoveryResultsQuery(Guid.NewGuid()));

        (await act.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator, Viewer");
    }

    [Fact]
    public async Task List_InvalidKind_ShouldThrowPtBrListingValidValues()
    {
        using var scope = CreateScope();
        UseRole(UserRole.Viewer);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var act = () => mediator.Send(
            new SearchDiscoveryResultsQuery(Guid.NewGuid(), Kind: "banana"));

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*Valores válidos: Neighbor, ArpEntry, MacTableEntry, DhcpLease, Host*");
    }

    [Fact]
    public async Task List_KindFilter_ShouldPaginateServerSideWithFilteredTotalCount()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.31");
        var runId = await SeedRunWithResultsOfTwoKindsAsync(dbContext, device.Id);
        UseRole(UserRole.Viewer);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var page = await mediator.Send(
            new SearchDiscoveryResultsQuery(runId, Kind: "Host", PageSize: 1));

        page.TotalCount.Should().Be(2, "totalCount respeita o filtro kind");
        page.Items.Should().ContainSingle();
        page.Items.Single().Kind.Should().Be(nameof(DiscoveryResultKind.Host));

        var semFiltro = await mediator.Send(new SearchDiscoveryResultsQuery(runId));
        semFiltro.TotalCount.Should().Be(3, "sem kind volta a lista completa");
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

    private static async Task<Guid> SeedRunWithResultsOfTwoKindsAsync(
        AppDbContext dbContext,
        Guid deviceId)
    {
        var run = DiscoveryRun.Start(deviceId);
        run.Complete();
        dbContext.DiscoveryRuns.Add(run);

        dbContext.DiscoveryResults.Add(DiscoveryResult.Create(
            run.Id, DiscoveryResultKind.Neighbor, "sw-core-01 via Gi0/1", null));
        dbContext.DiscoveryResults.Add(DiscoveryResult.Create(
            run.Id, DiscoveryResultKind.Host, "192.0.2.10 ativo", null));
        dbContext.DiscoveryResults.Add(DiscoveryResult.Create(
            run.Id, DiscoveryResultKind.Host, "192.0.2.11 ativo", null));
        await dbContext.SaveChangesAsync();
        return run.Id;
    }
}

using FluentAssertions;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Domain.Entities;
using InfraControl.IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Integrations;

/// <summary>
/// F27 Fase 3.1: repositório de hosts Zabbix sincronizados — upsert por
/// hostid, busca por nome/IP/hostid, filtro de ausência e paginação.
/// </summary>
public sealed class ZabbixHostsRepositoryTests : BaseIntegrationTest
{
    [Fact]
    public async Task AddAndGetByHostId_ShouldRoundTrip()
    {
        using var scope = CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<IZabbixHostsRepository>();
        var hostId = $"9{Guid.NewGuid():N}"[..12];
        var host = ZabbixHost.Create(
            hostId, $"sw-{hostId}", "Switch", "192.0.2.50", 10050,
            enabled: true, discovered: false,
            ["Switches"], ["Cisco IOS"], DateTime.UtcNow);

        await repository.AddAsync(host, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        var loaded = await repository.GetByHostIdAsync(hostId, CancellationToken.None);
        loaded.Should().NotBeNull();
        loaded!.Host.Should().StartWith("sw-");
        loaded.Ip.Should().Be("192.0.2.50");
        loaded.ReadGroups().Should().ContainSingle().Which.Should().Be("Switches");
        loaded.ReadTemplates().Should().ContainSingle().Which.Should().Be("Cisco IOS");
        loaded.MissingSinceUtc.Should().BeNull();
    }

    [Fact]
    public async Task Search_ShouldMatchByNameIpAndHostId_WithPaging()
    {
        using var scope = CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<IZabbixHostsRepository>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var ipTail = Random.Shared.Next(60, 200);
        await repository.AddAsync(ZabbixHost.Create(
            $"8{suffix}", $"srv-{suffix}", "Servidor", $"198.51.100.{ipTail}", null,
            true, false, [], [], DateTime.UtcNow), CancellationToken.None);
        await repository.AddAsync(ZabbixHost.Create(
            $"7{suffix}", $"cam-{suffix}", "Câmera", null, null,
            true, false, [], [], DateTime.UtcNow), CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        var byName = await repository.SearchAsync(
            $"srv-{suffix}", null, null, 1, 20, CancellationToken.None);
        byName.TotalCount.Should().Be(1);
        byName.Items.Single().Host.Should().Be($"srv-{suffix}");

        var byIp = await repository.SearchAsync(
            $"198.51.100.{ipTail}", null, null, 1, 20, CancellationToken.None);
        byIp.TotalCount.Should().Be(1);
        byIp.Items.Single().Host.Should().Be($"srv-{suffix}");

        var byHostId = await repository.SearchAsync(
            $"7{suffix}", null, null, 1, 20, CancellationToken.None);
        byHostId.TotalCount.Should().Be(1);
        byHostId.Items.Single().Host.Should().Be($"cam-{suffix}");

        var paged = await repository.SearchAsync(
            suffix, null, null, 1, 1, CancellationToken.None);
        paged.TotalCount.Should().Be(2);
        paged.Items.Should().HaveCount(1);
        paged.Page.Should().Be(1);
    }

    [Fact]
    public async Task Search_MissingFilter_ShouldSeparateAbsentHosts()
    {
        using var scope = CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<IZabbixHostsRepository>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var present = ZabbixHost.Create(
            $"6{suffix}", $"presente-{suffix}", "Presente", null, null,
            true, false, [], [], DateTime.UtcNow);
        var absent = ZabbixHost.Create(
            $"5{suffix}", $"ausente-{suffix}", "Ausente", null, null,
            true, false, [], [], DateTime.UtcNow);
        await repository.AddAsync(present, CancellationToken.None);
        await repository.AddAsync(absent, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);
        absent.MarkMissing();
        await repository.SaveChangesAsync(CancellationToken.None);

        var missing = await repository.SearchAsync(
            suffix, null, missing: true, 1, 20, CancellationToken.None);
        missing.TotalCount.Should().Be(1);
        missing.Items.Single().Host.Should().Be($"ausente-{suffix}");
        missing.Items.Single().MissingSinceUtc.Should().NotBeNull();

        var presentOnly = await repository.SearchAsync(
            suffix, null, missing: false, 1, 20, CancellationToken.None);
        presentOnly.TotalCount.Should().Be(1);
        presentOnly.Items.Single().Host.Should().Be($"presente-{suffix}");
    }

    [Fact]
    public async Task Update_ShouldOverwriteAndClearMissing()
    {
        using var scope = CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<IZabbixHostsRepository>();
        var hostId = $"4{Guid.NewGuid():N}"[..12];
        var host = ZabbixHost.Create(
            hostId, "velho", "Velho", "192.0.2.60", 10050,
            true, false, [], [], DateTime.UtcNow);
        await repository.AddAsync(host, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);
        host.MarkMissing();
        await repository.SaveChangesAsync(CancellationToken.None);

        host.Update(
            hostId, "novo", "Novo", "192.0.2.61", 10051,
            enabled: false, discovered: true,
            ["Linux servers"], ["Linux by Zabbix agent"], DateTime.UtcNow);
        await repository.SaveChangesAsync(CancellationToken.None);

        var loaded = await repository.GetByHostIdAsync(hostId, CancellationToken.None);
        loaded!.Host.Should().Be("novo");
        loaded.Ip.Should().Be("192.0.2.61");
        loaded.Enabled.Should().BeFalse();
        loaded.Discovered.Should().BeTrue();
        loaded.MissingSinceUtc.Should().BeNull();
        loaded.ReadGroups().Should().ContainSingle().Which.Should().Be("Linux servers");
    }
}

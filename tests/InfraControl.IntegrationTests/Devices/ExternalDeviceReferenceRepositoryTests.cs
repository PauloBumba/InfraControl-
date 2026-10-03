using FluentAssertions;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Devices;

/// <summary>
/// F19-T006: ExternalDeviceReference no PostgreSQL real — lookup
/// "MAC/Device ID → Device" (case-insensitive), separação por fonte e
/// unicidade física (source, external_id). A resolução do Zabbix por
/// IP/hostname não muda (regra 6 da revisão F19).
/// </summary>
public sealed class ExternalDeviceReferenceRepositoryTests : BaseIntegrationTest
{
    [Fact]
    public async Task GetDeviceId_WithStableId_ShouldResolveDevice()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = scope.ServiceProvider
            .GetRequiredService<IExternalDeviceReferenceRepository>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.40");
        var externalId = $"f4:92:bf:{Guid.NewGuid():N}";

        await repository.AddAsync(ExternalDeviceReference.Create(
            ExternalDeviceSource.UniFi, externalId, device.Id));
        await repository.SaveChangesAsync();

        var resolved = await repository.GetDeviceIdAsync(
            ExternalDeviceSource.UniFi, externalId);

        resolved.Should().Be(device.Id);
    }

    [Fact]
    public async Task GetDeviceId_ShouldIgnoreCaseAndWhitespace()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = scope.ServiceProvider
            .GetRequiredService<IExternalDeviceReferenceRepository>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.41");

        await repository.AddAsync(ExternalDeviceReference.Create(
            ExternalDeviceSource.UniFi, "AA:BB:CC:00:11:22", device.Id));
        await repository.SaveChangesAsync();

        var resolved = await repository.GetDeviceIdAsync(
            ExternalDeviceSource.UniFi, "  aa:bb:cc:00:11:22 ");

        resolved.Should().Be(device.Id);
    }

    [Fact]
    public async Task GetDeviceId_OnOtherSource_ShouldNotCrossMatch()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = scope.ServiceProvider
            .GetRequiredService<IExternalDeviceReferenceRepository>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.42");
        var externalId = $"host-{Guid.NewGuid():N}";

        await repository.AddAsync(ExternalDeviceReference.Create(
            ExternalDeviceSource.Zabbix, externalId, device.Id));
        await repository.SaveChangesAsync();

        var resolved = await repository.GetDeviceIdAsync(
            ExternalDeviceSource.UniFi, externalId);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task GetDeviceId_WithoutReference_ShouldReturnNull()
    {
        using var scope = CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<IExternalDeviceReferenceRepository>();

        var resolved = await repository.GetDeviceIdAsync(
            ExternalDeviceSource.Zabbix, $"sem-ref-{Guid.NewGuid():N}");

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task Add_DuplicateSourceAndExternalId_ShouldFailOnUniqueIndex()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = scope.ServiceProvider
            .GetRequiredService<IExternalDeviceReferenceRepository>();
        var first = await SeedDeviceAsync(dbContext, ip: "192.0.2.43", name: "dup-a");
        var second = await SeedDeviceAsync(dbContext, ip: "192.0.2.44", name: "dup-b");
        var externalId = $"dup-{Guid.NewGuid():N}";

        await repository.AddAsync(ExternalDeviceReference.Create(
            ExternalDeviceSource.UniFi, externalId, first.Id));
        await repository.SaveChangesAsync();

        await repository.AddAsync(ExternalDeviceReference.Create(
            ExternalDeviceSource.UniFi, externalId, second.Id));

        var save = () => repository.SaveChangesAsync();

        var exception = await save.Should().ThrowAsync<DbUpdateException>();

        exception.And.InnerException!.Message
            .Should().Contain("IX_external_device_references_source_external_id");
    }
}

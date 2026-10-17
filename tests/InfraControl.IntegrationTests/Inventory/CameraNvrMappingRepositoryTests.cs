using FluentAssertions;
using InfraControl.Application.Inventory.CameraNvrMappings.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace InfraControl.IntegrationTests.Inventory;

/// <summary>
/// F30-T005c: CameraNvrMapping no PostgreSQL real — round-trip do repositório,
/// filtros de busca, backstop do índice único (nvr_id, channel_number) e
/// comportamento das FKs (camera Cascade / nvr Restrict).
/// </summary>
public sealed class CameraNvrMappingRepositoryTests : BaseIntegrationTest
{
    private async Task<(Device Camera, Device Nvr)> SeedCameraAndNvrAsync(AppDbContext dbContext)
    {
        var location = Location.Create("Sala Integracao");
        dbContext.Locations.Add(location);
        await dbContext.SaveChangesAsync();

        var camera = Device.Create(location.Id, "cam-front-01", DeviceType.Camera);
        var nvr = Device.Create(location.Id, "nvr-01", DeviceType.Nvr);
        dbContext.Devices.AddRange(camera, nvr);
        await dbContext.SaveChangesAsync();
        return (camera, nvr);
    }

    [Fact]
    public async Task AddAndGetById_ShouldRoundTrip()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = scope.ServiceProvider
            .GetRequiredService<ICameraNvrMappingRepository>();
        var (camera, nvr) = await SeedCameraAndNvrAsync(dbContext);

        var mapping = CameraNvrMapping.Create(camera.Id, nvr.Id, 4);
        await repository.AddAsync(mapping);
        await repository.SaveChangesAsync();

        var loaded = await repository.GetByIdAsync(mapping.Id);

        loaded.Should().NotBeNull();
        loaded!.CameraId.Should().Be(camera.Id);
        loaded.NvrId.Should().Be(nvr.Id);
        loaded.ChannelNumber.Should().Be(4);
    }

    [Fact]
    public async Task GetByNvrAndChannel_ShouldHitAndMiss()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = scope.ServiceProvider
            .GetRequiredService<ICameraNvrMappingRepository>();
        var (camera, nvr) = await SeedCameraAndNvrAsync(dbContext);
        var mapping = CameraNvrMapping.Create(camera.Id, nvr.Id, 7);
        await repository.AddAsync(mapping);
        await repository.SaveChangesAsync();

        var hit = await repository.GetByNvrAndChannelAsync(nvr.Id, 7);
        var miss = await repository.GetByNvrAndChannelAsync(nvr.Id, 8);

        hit.Should().NotBeNull();
        hit!.Id.Should().Be(mapping.Id);
        miss.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterByNvrAndCamera()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = scope.ServiceProvider
            .GetRequiredService<ICameraNvrMappingRepository>();
        var (camera, nvr) = await SeedCameraAndNvrAsync(dbContext);
        await repository.AddAsync(CameraNvrMapping.Create(camera.Id, nvr.Id, 1));
        await repository.AddAsync(CameraNvrMapping.Create(camera.Id, nvr.Id, 2));
        await repository.SaveChangesAsync();

        var byNvr = await repository.SearchAsync(nvr.Id, null, 1, 20);
        var byCamera = await repository.SearchAsync(null, camera.Id, 1, 20);
        var empty = await repository.SearchAsync(Guid.NewGuid(), null, 1, 20);

        byNvr.TotalCount.Should().Be(2);
        byNvr.Items.Should().OnlyContain(mapping => mapping.NvrId == nvr.Id);
        byCamera.TotalCount.Should().Be(2);
        byCamera.Items.Should().BeInAscendingOrder(mapping => mapping.ChannelNumber);
        empty.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Add_DuplicateNvrAndChannel_ShouldFailOnUniqueIndex()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = scope.ServiceProvider
            .GetRequiredService<ICameraNvrMappingRepository>();
        var (camera, nvr) = await SeedCameraAndNvrAsync(dbContext);

        await repository.AddAsync(CameraNvrMapping.Create(camera.Id, nvr.Id, 1));
        await repository.SaveChangesAsync();

        await repository.AddAsync(CameraNvrMapping.Create(camera.Id, nvr.Id, 1));

        var save = () => repository.SaveChangesAsync();
        var exception = await save.Should().ThrowAsync<DbUpdateException>();

        exception.And.InnerException!.Message
            .Should().Contain("ux_camera_nvr_mappings_nvr_channel");
    }

    [Fact]
    public async Task DeleteCamera_ShouldCascadeRemoveMapping()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = scope.ServiceProvider
            .GetRequiredService<ICameraNvrMappingRepository>();
        var (camera, nvr) = await SeedCameraAndNvrAsync(dbContext);
        var mapping = CameraNvrMapping.Create(camera.Id, nvr.Id, 1);
        await repository.AddAsync(mapping);
        await repository.SaveChangesAsync();

        dbContext.Devices.Remove(camera);
        await dbContext.SaveChangesAsync();

        var loaded = await repository.GetByIdAsync(mapping.Id);
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task DeleteNvrWithMapping_ShouldFailOnRestrictForeignKey()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = scope.ServiceProvider
            .GetRequiredService<ICameraNvrMappingRepository>();
        var (camera, nvr) = await SeedCameraAndNvrAsync(dbContext);
        await repository.AddAsync(CameraNvrMapping.Create(camera.Id, nvr.Id, 1));
        await repository.SaveChangesAsync();

        // Backstop do banco: o handler do DeleteDevice devolve 409 amigável
        // antes; a FK RESTRICT de nvr_id bloqueia a exclusão direta (23503).
        var delete = () => dbContext.Database.ExecuteSqlAsync(
            $"DELETE FROM devices WHERE id = {nvr.Id}");

        var exception = await delete.Should().ThrowAsync<PostgresException>();
        exception.And.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        exception.And.ConstraintName.Should().Be("FK_camera_nvr_mappings_devices_nvr_id");
    }
}

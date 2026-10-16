using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F30-T005c: filtros por NVR/câmera e saneamento de paginação do repositório
/// CameraNvrMapping (mesmo molde de RackSearchTests / F29-T001A).
/// </summary>
public sealed class CameraNvrMappingSearchTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public CameraNvrMappingSearchTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<(AppDbContext Context, Guid CameraId, Guid NvrId)> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var location = Location.Create("Data Center A");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var camera = Device.Create(location.Id, "cam-front-01", DeviceType.Camera);
        var nvr = Device.Create(location.Id, "nvr-01", DeviceType.Nvr);
        context.Devices.AddRange(camera, nvr);
        await context.SaveChangesAsync();

        context.CameraNvrMappings.AddRange(
            CameraNvrMapping.Create(camera.Id, nvr.Id, 1),
            CameraNvrMapping.Create(camera.Id, nvr.Id, 2));
        await context.SaveChangesAsync();

        return (context, camera.Id, nvr.Id);
    }

    [Fact]
    public async Task SearchAsync_WithNonsensePaging_ShouldClampInsteadOfReturningEmpty()
    {
        var (context, _, _) = await CreateContextAsync();
        var repository = new CameraNvrMappingRepository(context);

        var page = await repository.SearchAsync(
            nvrId: null, cameraId: null, page: 0, pageSize: 0);

        page.TotalCount.Should().Be(2);
        page.Page.Should().Be(1);
        page.PageSize.Should().BeGreaterThanOrEqualTo(1);
        page.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ByNvr_ShouldReturnOnlyItsChannels()
    {
        var (context, cameraId, nvrId) = await CreateContextAsync();
        var otherNvr = Device.Create(
            context.Locations.AsNoTracking().First().Id, "nvr-02", DeviceType.Nvr);
        context.Devices.Add(otherNvr);
        await context.SaveChangesAsync();
        var repository = new CameraNvrMappingRepository(context);

        var page = await repository.SearchAsync(
            nvrId: nvrId, cameraId: null, page: 1, pageSize: 20);

        page.TotalCount.Should().Be(2);
        page.Items.Should().OnlyContain(mapping =>
            mapping.NvrId == nvrId && mapping.CameraId == cameraId);
        page.Items.Should().BeInAscendingOrder(mapping => mapping.ChannelNumber);
    }

    [Fact]
    public async Task SearchAsync_ByCamera_ShouldReturnOnlyItsMappings()
    {
        var (context, cameraId, _) = await CreateContextAsync();
        var repository = new CameraNvrMappingRepository(context);

        var page = await repository.SearchAsync(
            nvrId: null, cameraId: cameraId, page: 1, pageSize: 20);

        page.TotalCount.Should().Be(2);
        page.Items.Should().OnlyContain(mapping => mapping.CameraId == cameraId);
    }

    [Fact]
    public async Task SearchAsync_ByUnknownCamera_ShouldReturnEmpty()
    {
        var (context, _, _) = await CreateContextAsync();
        var repository = new CameraNvrMappingRepository(context);

        var page = await repository.SearchAsync(
            nvrId: null, cameraId: Guid.NewGuid(), page: 1, pageSize: 20);

        page.TotalCount.Should().Be(0);
        page.Items.Should().BeEmpty();
    }
}

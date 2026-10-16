using FluentAssertions;
using InfraControl.Application.Inventory.CameraNvrMappings.Commands;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F30-T005c: handlers do slice CameraNvrMapping com repositório real em
/// Sqlite. Mapeamento esperado no envelope: ArgumentException → 400,
/// KeyNotFound → 404, InvalidOperationException → 409 (canal repetido no NVR).
/// Validations de tipo: câmera precisa ser DeviceType.Camera, NVR precisa ser
/// DeviceType.Nvr (docs/46 §Modelo de domínio).
/// </summary>
public sealed class CameraNvrMappingCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public CameraNvrMappingCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static async Task<Guid> SeedLocationAsync(AppDbContext context, string name = "Data Center A")
    {
        var location = Location.Create(name);
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        return location.Id;
    }

    private static async Task<(Guid CameraId, Guid NvrId)> SeedDevicesAsync(AppDbContext context)
    {
        var locationId = await SeedLocationAsync(context);
        var camera = Device.Create(locationId, "cam-front-01", DeviceType.Camera);
        var nvr = Device.Create(locationId, "nvr-01", DeviceType.Nvr);
        context.Devices.AddRange(camera, nvr);
        await context.SaveChangesAsync();
        return (camera.Id, nvr.Id);
    }

    private static CreateCameraNvrMappingCommandHandler CreateHandler(AppDbContext context)
        => new(
            new CameraNvrMappingRepository(context),
            new DeviceRepository(context),
            NullLogger<CreateCameraNvrMappingCommandHandler>.Instance);

    private static UpdateCameraNvrMappingCommandHandler UpdateHandler(AppDbContext context)
        => new(
            new CameraNvrMappingRepository(context),
            new DeviceRepository(context),
            NullLogger<UpdateCameraNvrMappingCommandHandler>.Instance);

    private static DeleteCameraNvrMappingCommandHandler DeleteHandler(AppDbContext context)
        => new(
            new CameraNvrMappingRepository(context),
            NullLogger<DeleteCameraNvrMappingCommandHandler>.Instance);

    // ── Create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ShouldPersistAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var (cameraId, nvrId) = await SeedDevicesAsync(context);

        var result = await CreateHandler(context).Handle(
            new CreateCameraNvrMappingCommand(cameraId, nvrId, 5),
            CancellationToken.None);

        result.Id.Should().NotBe(Guid.Empty);
        result.CameraId.Should().Be(cameraId);
        result.NvrId.Should().Be(nvrId);
        result.ChannelNumber.Should().Be(5);
        context.CameraNvrMappings.Should()
            .ContainSingle(mapping => mapping.Id == result.Id);
    }

    [Fact]
    public async Task Create_UnknownCamera_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var (_, nvrId) = await SeedDevicesAsync(context);
        var unknownCameraId = Guid.NewGuid();

        var action = () => CreateHandler(context).Handle(
            new CreateCameraNvrMappingCommand(unknownCameraId, nvrId, 1),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Câmera '{unknownCameraId}' não encontrada.");
    }

    [Fact]
    public async Task Create_DeviceThatIsNotCamera_ShouldThrowConflictTypeMessage()
    {
        await using var context = await CreateContextAsync();
        var (_, nvrId) = await SeedDevicesAsync(context);
        var locationId = await SeedLocationAsync(context, "Outro");
        var switchDevice = Device.Create(locationId, "sw-01", DeviceType.Switch);
        context.Devices.Add(switchDevice);
        await context.SaveChangesAsync();

        var action = () => CreateHandler(context).Handle(
            new CreateCameraNvrMappingCommand(switchDevice.Id, nvrId, 1),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Device 'sw-01' não é uma câmera.");
    }

    [Fact]
    public async Task Create_UnknownNvr_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var (cameraId, _) = await SeedDevicesAsync(context);
        var unknownNvrId = Guid.NewGuid();

        var action = () => CreateHandler(context).Handle(
            new CreateCameraNvrMappingCommand(cameraId, unknownNvrId, 1),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"NVR '{unknownNvrId}' não encontrado.");
    }

    [Fact]
    public async Task Create_DeviceThatIsNotNvr_ShouldThrowConflictTypeMessage()
    {
        await using var context = await CreateContextAsync();
        var (cameraId, _) = await SeedDevicesAsync(context);
        var locationId = await SeedLocationAsync(context, "Outro");
        var switchDevice = Device.Create(locationId, "sw-01", DeviceType.Switch);
        context.Devices.Add(switchDevice);
        await context.SaveChangesAsync();

        var action = () => CreateHandler(context).Handle(
            new CreateCameraNvrMappingCommand(cameraId, switchDevice.Id, 1),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Device 'sw-01' não é um NVR.");
    }

    [Fact]
    public async Task Create_DuplicateChannelOnSameNvr_ShouldThrowConflictPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var (cameraId, nvrId) = await SeedDevicesAsync(context);
        await CreateHandler(context).Handle(
            new CreateCameraNvrMappingCommand(cameraId, nvrId, 1), CancellationToken.None);

        var action = () => CreateHandler(context).Handle(
            new CreateCameraNvrMappingCommand(cameraId, nvrId, 1), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um mapeamento do canal 1 no NVR 'nvr-01'.");
    }

    [Fact]
    public async Task Create_InvalidChannel_ShouldThrowDomainPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var (cameraId, nvrId) = await SeedDevicesAsync(context);

        var action = () => CreateHandler(context).Handle(
            new CreateCameraNvrMappingCommand(cameraId, nvrId, 0), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O número do canal deve ser maior ou igual a 1.*");
    }

    // ── Update ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ShouldMoveCameraToAnotherChannel()
    {
        await using var context = await CreateContextAsync();
        var (cameraId, nvrId) = await SeedDevicesAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateCameraNvrMappingCommand(cameraId, nvrId, 1), CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateCameraNvrMappingCommand(created.Id, cameraId, nvrId, 8),
            CancellationToken.None);

        var persisted = context.CameraNvrMappings
            .AsNoTracking()
            .Single(mapping => mapping.Id == created.Id);
        persisted.ChannelNumber.Should().Be(8);
    }

    [Fact]
    public async Task Update_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var (cameraId, nvrId) = await SeedDevicesAsync(context);
        var unknownId = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(
            new UpdateCameraNvrMappingCommand(unknownId, cameraId, nvrId, 2),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Mapeamento '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Update_KeepingOwnChannel_ShouldNotConflictWithItself()
    {
        await using var context = await CreateContextAsync();
        var (cameraId, nvrId) = await SeedDevicesAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateCameraNvrMappingCommand(cameraId, nvrId, 3), CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateCameraNvrMappingCommand(created.Id, cameraId, nvrId, 3),
            CancellationToken.None);

        context.CameraNvrMappings.Should().ContainSingle();
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingMapping_ShouldRemoveIt()
    {
        await using var context = await CreateContextAsync();
        var (cameraId, nvrId) = await SeedDevicesAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateCameraNvrMappingCommand(cameraId, nvrId, 1), CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteCameraNvrMappingCommand(created.Id), CancellationToken.None);

        context.CameraNvrMappings.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => DeleteHandler(context).Handle(
            new DeleteCameraNvrMappingCommand(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Mapeamento '{unknownId}' não encontrado.");
    }
}

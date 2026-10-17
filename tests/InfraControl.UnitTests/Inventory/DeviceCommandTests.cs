using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices;
using InfraControl.Application.Inventory.Devices.Commands;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Devices.Queries;
using InfraControl.Application.Inventory.Locations;
using InfraControl.Application.Inventory.Locations.Commands;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Application.Inventory.Racks;
using InfraControl.Application.Inventory.Racks.Commands;
using InfraControl.Application.Inventory.Racks.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F2-T003: handlers do slice Device com repositório real em Sqlite.
/// Mapeamento no envelope: ArgumentException → 400, KeyNotFound → 404,
/// InvalidOperationException → 409. Nome único global (case-insensitive).
/// </summary>
public sealed class DeviceCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public DeviceCommandTests()
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

    private static async Task<Guid> SeedRackAsync(AppDbContext context, Guid locationId, string name = "R01")
    {
        var rack = Rack.Create(locationId, name);
        context.Racks.Add(rack);
        await context.SaveChangesAsync();
        return rack.Id;
    }

    private static CreateDeviceCommandHandler CreateHandler(AppDbContext context)
        => new(
            new DeviceRepository(context),
            new LocationRepository(context),
            new RackRepository(context),
            new MonitoringCheckRepository(context),
            NullLogger<CreateDeviceCommandHandler>.Instance);

    private static UpdateDeviceCommandHandler UpdateHandler(AppDbContext context)
        => new(
            new DeviceRepository(context),
            new LocationRepository(context),
            new RackRepository(context),
            NullLogger<UpdateDeviceCommandHandler>.Instance);

    private static DeleteDeviceCommandHandler DeleteHandler(AppDbContext context)
        => new(new DeviceRepository(context), NullLogger<DeleteDeviceCommandHandler>.Instance);

    // ── Create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ShouldPersistAndReturnDto()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);

        var result = await CreateHandler(context).Handle(
            new CreateDeviceCommand(
                locationId, "sw-core-01", "Switch",
                Ip: "192.0.2.10", Mac: "aa:bb:cc:dd:ee:ff"),
            CancellationToken.None);

        result.Id.Should().NotBe(Guid.Empty);
        result.LocationId.Should().Be(locationId);
        result.Name.Should().Be("sw-core-01");
        result.Type.Should().Be("Switch");
        result.Ip.Should().Be("192.0.2.10");
        context.Devices.Should().ContainSingle(device => device.Id == result.Id);
    }

    [Fact]
    public async Task Create_UnknownLocation_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownLocation = Guid.NewGuid();

        var action = () => CreateHandler(context).Handle(
            new CreateDeviceCommand(unknownLocation, "sw-01", "Switch"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Local '{unknownLocation}' não encontrado.");
    }

    [Fact]
    public async Task Create_WithUnknownRack_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var unknownRack = Guid.NewGuid();

        var action = () => CreateHandler(context).Handle(
            new CreateDeviceCommand(locationId, "sw-01", "Switch", RackId: unknownRack),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Rack '{unknownRack}' não encontrado.");
    }

    [Fact]
    public async Task Create_WithRackFromAnotherLocation_ShouldThrowConflictPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var locationA = await SeedLocationAsync(context, "Sala A");
        var locationB = await SeedLocationAsync(context, "Sala B");
        var rackInB = await SeedRackAsync(context, locationB, "R01");

        var action = () => CreateHandler(context).Handle(
            new CreateDeviceCommand(locationA, "sw-01", "Switch", RackId: rackInB),
            CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("O rack não pertence ao local informado.");
    }

    [Fact]
    public async Task Create_DuplicateName_ShouldThrowConflictPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        await CreateHandler(context).Handle(
            new CreateDeviceCommand(locationId, "sw-01", "Switch"), CancellationToken.None);

        var action = () => CreateHandler(context).Handle(
            new CreateDeviceCommand(locationId, "SW-01", "Switch"), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um device com o nome 'SW-01'.");
    }

    [Fact]
    public async Task Create_InvalidType_ShouldThrowArgumentPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);

        var action = () => CreateHandler(context).Handle(
            new CreateDeviceCommand(locationId, "sw-01", "Banana"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Tipo de device inválido*");
    }

    // ── Update ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ShouldApplyChanges()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateDeviceCommand(locationId, "sw-01", "Switch"), CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateDeviceCommand(
                created.Id, "sw-02", "Router", Ip: "192.0.2.11"),
            CancellationToken.None);

        var stored = context.Devices.Single(device => device.Id == created.Id);
        stored.Name.Should().Be("sw-02");
        stored.Type.Should().Be(DeviceType.Router);
        stored.Ip.Should().Be("192.0.2.11");
    }

    [Fact]
    public async Task Update_ToAnotherDeviceName_ShouldThrowConflictPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        await CreateHandler(context).Handle(
            new CreateDeviceCommand(locationId, "sw-01", "Switch"), CancellationToken.None);
        var second = await CreateHandler(context).Handle(
            new CreateDeviceCommand(locationId, "sw-02", "Switch"), CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateDeviceCommand(second.Id, "SW-01", "Switch"), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um device com o nome 'SW-01'.");
    }

    [Fact]
    public async Task Update_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(
            new UpdateDeviceCommand(unknownId, "sw-01", "Switch"), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }

    // ── F30-T006c (docs/47 §A): extensão/SIP do telefone ─────────────────

    [Fact]
    public async Task Create_Phone_ShouldPersistExtensionAndSipServer()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);

        var result = await CreateHandler(context).Handle(
            new CreateDeviceCommand(
                locationId, "phone-01", "Phone",
                Extension: "1001", SipServer: "pbx.local"),
            CancellationToken.None);

        result.Extension.Should().Be("1001");
        result.SipServer.Should().Be("pbx.local");

        var stored = context.Devices.Single(device => device.Id == result.Id);
        stored.Extension.Should().Be("1001");
        stored.SipServer.Should().Be("pbx.local");
    }

    [Fact]
    public async Task Update_Phone_ShouldClearExtensionAndSipServerWhenAbsent()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateDeviceCommand(
                locationId, "phone-02", "Phone",
                Extension: "1002", SipServer: "pbx.local"),
            CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateDeviceCommand(created.Id, "phone-02", "Phone"),
            CancellationToken.None);

        var stored = context.Devices.Single(device => device.Id == created.Id);
        stored.Extension.Should().BeNull();
        stored.SipServer.Should().BeNull();
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingDevice_ShouldRemoveIt()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateDeviceCommand(locationId, "sw-01", "Switch"), CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteDeviceCommand(created.Id), CancellationToken.None);

        context.Devices.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => DeleteHandler(context).Handle(
            new DeleteDeviceCommand(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Delete_DeviceWithInterfaces_ShouldThrowConflictMessage()
    {
        // F2-T004: fecha a dependência anotada na F2-T003.
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateDeviceCommand(locationId, "sw-01", "Switch"), CancellationToken.None);
        context.NetworkInterfaces.Add(NetworkInterface.Create(created.Id, "eth0"));
        await context.SaveChangesAsync();

        var action = () => DeleteHandler(context).Handle(
            new DeleteDeviceCommand(created.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir o device: existem interfaces vinculadas.");
    }

    [Fact]
    public async Task Delete_SwitchWithPorts_ShouldThrowConflictMessage()
    {
        // F3-T002: fecha a cadeia Device → SwitchPort (portas vêm antes de
        // interfaces no handler por serem específicas de switch).
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateDeviceCommand(locationId, "sw-01", "Switch"), CancellationToken.None);
        context.SwitchPorts.Add(SwitchPort.Create(created.Id, "Gi1/0/1"));
        await context.SaveChangesAsync();

        var action = () => DeleteHandler(context).Handle(
            new DeleteDeviceCommand(created.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir o device: existem portas vinculadas.");
    }

    [Fact]
    public async Task Delete_NvrWithCameraMappings_ShouldThrowConflictMessage()
    {
        // F30-T005c: FK Restrict de nvr_id — o pre-check do handler devolve 409
        // em vez de deixar estourar violação de FK (500).
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var camera = Device.Create(locationId, "cam-01", DeviceType.Camera);
        var nvr = Device.Create(locationId, "nvr-01", DeviceType.Nvr);
        context.Devices.AddRange(camera, nvr);
        await context.SaveChangesAsync();
        context.CameraNvrMappings.Add(CameraNvrMapping.Create(camera.Id, nvr.Id, 1));
        await context.SaveChangesAsync();

        var action = () => DeleteHandler(context).Handle(
            new DeleteDeviceCommand(nvr.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir o device: existem mapeamentos câmera-NVR vinculados.");
        context.Devices.AsNoTracking().Should().ContainSingle(device => device.Id == nvr.Id);
    }

    [Fact]
    public async Task Delete_CameraWithMapping_ShouldCascadeRemoveMapping()
    {
        // F30-T005c: FK Cascade de camera_id — excluir a câmera remove o vínculo.
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var camera = Device.Create(locationId, "cam-01", DeviceType.Camera);
        var nvr = Device.Create(locationId, "nvr-01", DeviceType.Nvr);
        context.Devices.AddRange(camera, nvr);
        await context.SaveChangesAsync();
        context.CameraNvrMappings.Add(CameraNvrMapping.Create(camera.Id, nvr.Id, 1));
        await context.SaveChangesAsync();

        await DeleteHandler(context).Handle(
            new DeleteDeviceCommand(camera.Id), CancellationToken.None);

        context.CameraNvrMappings.Should().BeEmpty();
        context.Devices.AsNoTracking().Should().ContainSingle(device => device.Id == nvr.Id);
    }

    // ── Queries ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_ShouldFilterByLocationAndType()
    {
        await using var context = await CreateContextAsync();
        var locationA = await SeedLocationAsync(context, "Sala A");
        var locationB = await SeedLocationAsync(context, "Sala B");
        await CreateHandler(context).Handle(
            new CreateDeviceCommand(locationA, "sw-01", "Switch"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateDeviceCommand(locationA, "prn-01", "Printer"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateDeviceCommand(locationB, "sw-02", "Switch"), CancellationToken.None);

        var handler = new SearchDevicesQueryHandler(new DeviceRepository(context));
        var byLocation = await handler.Handle(
            new SearchDevicesQuery(LocationId: locationA), CancellationToken.None);
        var byType = await handler.Handle(
            new SearchDevicesQuery(Type: "Printer"), CancellationToken.None);
        var all = await handler.Handle(new SearchDevicesQuery(), CancellationToken.None);

        byLocation.TotalCount.Should().Be(2);
        byType.TotalCount.Should().Be(1);
        byType.Items.Single().Name.Should().Be("prn-01");
        all.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Search_ShouldFilterByTermAcrossNameIpSerial()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        await CreateHandler(context).Handle(
            new CreateDeviceCommand(locationId, "sw-core-01", "Switch", Ip: "192.0.2.10"),
            CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateDeviceCommand(locationId, "prn-01", "Printer", Serial: "SN-PRN-9"),
            CancellationToken.None);

        var handler = new SearchDevicesQueryHandler(new DeviceRepository(context));
        var byName = await handler.Handle(
            new SearchDevicesQuery(Term: "CORE"), CancellationToken.None);
        var byIp = await handler.Handle(
            new SearchDevicesQuery(Term: "192.0.2"), CancellationToken.None);
        var bySerial = await handler.Handle(
            new SearchDevicesQuery(Term: "sn-prn"), CancellationToken.None);

        byName.TotalCount.Should().Be(1);
        byIp.TotalCount.Should().Be(1);
        bySerial.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetById_ExistingDevice_ShouldReturnDto()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var created = await CreateHandler(context).Handle(
            new CreateDeviceCommand(locationId, "sw-01", "Switch", Ip: "192.0.2.10"),
            CancellationToken.None);

        var handler = new GetDeviceQueryHandler(new DeviceRepository(context));
        var result = await handler.Handle(new GetDeviceQuery(created.Id), CancellationToken.None);

        result.Id.Should().Be(created.Id);
        result.Ip.Should().Be("192.0.2.10");
        result.Type.Should().Be("Switch");
    }

    [Fact]
    public async Task GetById_UnknownId_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var handler = new GetDeviceQueryHandler(new DeviceRepository(context));
        var action = () => handler.Handle(new GetDeviceQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }
}

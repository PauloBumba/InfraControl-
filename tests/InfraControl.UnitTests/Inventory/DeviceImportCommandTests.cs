using FluentAssertions;
using InfraControl.Application.Inventory.Devices.Commands;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F50-T008: importação em massa de devices por CSV (colunas padronizadas,
/// separador `;` ou `,`). Sucesso parcial linha a linha: cria o que é válido,
/// pula nome já existente (re-importação idempotente) e reporta erro por
/// linha sem abortar o arquivo — e cada device criado materializa o perfil
/// de checks do tipo (ADR-014 §4), igual ao create manual.
/// </summary>
public sealed class DeviceImportCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public DeviceImportCommandTests()
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

    private static ImportDevicesCommandHandler Handler(AppDbContext context) => new(
        new DeviceRepository(context),
        new LocationRepository(context),
        new MonitoringCheckRepository(context),
        NullLogger<ImportDevicesCommandHandler>.Instance);

    private const string Header = "nome;ip;tipo;vendor;modelo;mac;serial;descricao";

    [Fact]
    public async Task Import_ValidRows_ShouldCreateDevicesAndMaterializeProfileChecks()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);

        var csv = string.Join("\n",
            Header,
            "Servidor Web-01;10.0.0.10;Server;Dell;PowerEdge R640;aa:bb:cc:dd:ee:01;SRV001;App",
            "Impressora RH;10.0.0.50;Printer;HP;LaserJet Pro;;IMP001;Toner 78%");

        var result = await Handler(context).Handle(
            new ImportDevicesCommand(locationId, csv), CancellationToken.None);

        result.Created.Should().Be(2);
        result.Skipped.Should().Be(0);
        result.Failed.Should().Be(0);
        result.Rows.Should().OnlyContain(row => row.Status == "created");
        context.Devices.Should().HaveCount(2);

        // Perfil do tipo materializado (ADR-014): impressora nasce com
        // Ping + SnmpToner + SnmpDrum — a UI de consumíveis depende disso.
        var printer = context.Devices.Single(device => device.Name == "Impressora RH");
        printer.Type.Should().Be(DeviceType.Printer);
        var kinds = context.MonitoringChecks
            .Where(check => check.DeviceId == printer.Id)
            .Select(check => check.Kind)
            .ToList();
        kinds.Should().Contain(CheckKind.SnmpToner).And.Contain(CheckKind.SnmpDrum);
    }

    [Fact]
    public async Task Import_ExistingName_ShouldSkipWithoutDuplicating()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        context.Devices.Add(Device.Create(locationId, "sw-core-01", DeviceType.Switch));
        await context.SaveChangesAsync();

        var csv = string.Join("\n", Header, "SW-CORE-01;192.0.2.10;Switch;;");

        var result = await Handler(context).Handle(
            new ImportDevicesCommand(locationId, csv), CancellationToken.None);

        result.Skipped.Should().Be(1);
        result.Created.Should().Be(0);
        result.Failed.Should().Be(0);
        result.Rows[0]!.Status.Should().Be("skipped");
        result.Rows[0]!.Message.Should().Contain("existe um device com o nome");
        context.Devices.Should().HaveCount(1);
    }

    [Fact]
    public async Task Import_InvalidTypeRow_ShouldFailOnlyThatRow()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);

        var csv = string.Join("\n",
            Header,
            "Servidor App;10.0.0.20;Server;;;",
            "Foguete;10.0.0.99;Foguete;;;");

        var result = await Handler(context).Handle(
            new ImportDevicesCommand(locationId, csv), CancellationToken.None);

        result.Created.Should().Be(1);
        result.Failed.Should().Be(1);
        var failed = result.Rows.Single(row => row.Status == "error");
        failed.Line.Should().Be(3);
        failed.Message.Should().Contain("Tipo de device inválido");
        context.Devices.Should().ContainSingle(device => device.Name == "Servidor App");
    }

    [Fact]
    public async Task Import_InvalidIpRow_ShouldFailThatRow()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);

        var csv = string.Join("\n",
            Header,
            "Servidor OK;10.0.0.20;Server;;;",
            "Servidor Ruim;999.1.1.1;Server;;;");

        var result = await Handler(context).Handle(
            new ImportDevicesCommand(locationId, csv), CancellationToken.None);

        result.Created.Should().Be(1);
        result.Failed.Should().Be(1);
        result.Rows[1]!.Status.Should().Be("error");
        result.Rows[1]!.Message.Should().Contain("IP");
        context.Devices.Should().ContainSingle(device => device.Name == "Servidor OK");
    }

    [Fact]
    public async Task Import_DuplicateNameInsideFile_ShouldFailTheSecondRow()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);

        var csv = string.Join("\n",
            Header,
            "Servidor App;10.0.0.20;Server;;;",
            "servidor app;10.0.0.21;Server;;;");

        var result = await Handler(context).Handle(
            new ImportDevicesCommand(locationId, csv), CancellationToken.None);

        result.Created.Should().Be(1);
        result.Failed.Should().Be(1);
        result.Rows[1]!.Message.Should().Contain("repetido no arquivo");
        context.Devices.Should().ContainSingle();
    }

    [Fact]
    public async Task Import_CommaSeparatedHeader_ShouldWork()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);

        var csv = "nome,ip,tipo\nServidor Comma,10.0.0.30,Server";

        var result = await Handler(context).Handle(
            new ImportDevicesCommand(locationId, csv), CancellationToken.None);

        result.Created.Should().Be(1);
        context.Devices.Single().Ip.Should().Be("10.0.0.30");
    }

    [Fact]
    public async Task Import_MissingRequiredColumns_ShouldThrowPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);

        var act = () => Handler(context).Handle(
            new ImportDevicesCommand(locationId, "nome;ip\nweb;10.0.0.1"),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.WithMessage("*nome*").And.Message.Should().Contain("tipo");
    }

    [Fact]
    public async Task Import_EmptyCsv_ShouldThrow()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);

        var act = () => Handler(context).Handle(
            new ImportDevicesCommand(locationId, "   \n  "),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*vazio*");
    }

    [Fact]
    public async Task Import_UnknownLocation_ShouldThrowNotFound()
    {
        await using var context = await CreateContextAsync();

        var act = () => Handler(context).Handle(
            new ImportDevicesCommand(Guid.NewGuid(), Header + "\nweb;10.0.0.1;Server"),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Import_BomHeader_ShouldStillMatchColumns()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);

        // O modelo baixado no dialog traz BOM (Excel) — o cabeçalho precisa
        // casar mesmo assim.
        var csv = "\uFEFF" + Header + "\nServidor BOM;10.0.0.40;Server;;;";

        var result = await Handler(context).Handle(
            new ImportDevicesCommand(locationId, csv), CancellationToken.None);

        result.Created.Should().Be(1);
    }
}

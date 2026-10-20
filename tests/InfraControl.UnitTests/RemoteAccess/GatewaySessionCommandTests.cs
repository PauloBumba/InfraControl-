using FluentAssertions;
using InfraControl.Application.RemoteAccess.Sessions;
using InfraControl.Application.RemoteAccess.Sessions.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.Domain.RemoteAccess;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.RemoteAccess;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.RemoteAccess;

/// <summary>
/// F34-T007 (ADR-060 D1/D2/D4): OpenRemoteGatewaySessionCommand — valida
/// device (IP + SO informado) e credencial, minta token SSO no gateway via
/// IGuacamoleGateway (header auth server-to-server) e registra a
/// RemoteSession (histórico/encerramento reaproveitam o fluxo F11). O
/// navegador recebe só {token, path}; credencial nunca vira DTO (D4).
/// </summary>
public sealed class GatewaySessionCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public GatewaySessionCommandTests()
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

    private static async Task<Domain.Entities.Device> SeedDeviceAsync(
        AppDbContext context,
        string? ip = "192.0.2.90",
        DeviceOperatingSystem? operatingSystem = DeviceOperatingSystem.Windows)
    {
        var location = Domain.Entities.Location.Create("Sala Gateway");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Domain.Entities.Device.Create(
            location.Id, $"srv-gw-{Guid.NewGuid():N}"[..13], DeviceType.Server,
            ip: ip, operatingSystem: operatingSystem);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    private static OpenRemoteGatewaySessionCommandHandler Handler(
        AppDbContext context,
        IGuacamoleGateway gateway,
        IRemoteCredentialSource credentialSource,
        Guid? userId = null)
        => new(
            new DeviceRepository(context),
            new RemoteSessionRepository(context),
            credentialSource,
            gateway,
            new FakeCurrentUser(userId ?? Guid.NewGuid()));

    // ── fakes ────────────────────────────────────────────────────────────

    private sealed class FakeGateway : IGuacamoleGateway
    {
        private readonly string? _error;

        public FakeGateway(string? error = null) => _error = error;

        public string? LastUserName { get; private set; }

        public string? EnsuredDeviceName { get; private set; }
        public string? EnsuredHostname { get; private set; }
        public RemoteProtocol? EnsuredProtocol { get; private set; }
        public string? EnsuredGrantToUser { get; private set; }

        public Task<string> CreateTokenAsync(
            string userName, CancellationToken cancellationToken = default)
        {
            LastUserName = userName;
            return _error is null
                ? Task.FromResult($"token-{Guid.NewGuid():N}")
                : throw new InvalidOperationException(_error);
        }

        public Task EnsureDeviceConnectionAsync(
            string deviceName,
            string hostname,
            RemoteProtocol protocol,
            RemoteCredential credential,
            string grantToUser,
            CancellationToken cancellationToken = default)
        {
            EnsuredDeviceName = deviceName;
            EnsuredHostname = hostname;
            EnsuredProtocol = protocol;
            EnsuredGrantToUser = grantToUser;
            return Task.CompletedTask;
        }
    }

    private sealed class ConfiguredCredentialSource : IRemoteCredentialSource
    {
        public Task<RemoteCredential> GetCredentialAsync(
            Guid deviceId, CancellationToken cancellationToken = default)
            => Task.FromResult(new RemoteCredential("it-gw", "S3nh@Unitária!42"));
    }

    private sealed class UnconfiguredCredentialSource : IRemoteCredentialSource
    {
        public Task<RemoteCredential> GetCredentialAsync(
            Guid deviceId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(
                "Credencial SSH não configurada. Defina RemoteAccess:Ssh:Username e RemoteAccess:Ssh:Password.");
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId;
        public string? UserName => "unit-test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => ["Admin"];
        public IReadOnlyCollection<string> Permissions => [];
    }

    // ── OpenRemoteGatewaySession ─────────────────────────────────────────

    [Fact]
    public async Task Open_WindowsDevice_ShouldMintTokenAndPersistRdpSession()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(
            context, operatingSystem: DeviceOperatingSystem.Windows);
        var gateway = new FakeGateway();
        var userId = Guid.NewGuid();

        var dto = await Handler(context, gateway, new ConfiguredCredentialSource(), userId)
            .Handle(new OpenRemoteGatewaySessionCommand(device.Id), CancellationToken.None);

        dto.Protocol.Should().Be("Rdp");
        dto.Token.Should().StartWith("token-");
        dto.Path.Should().Be("/guacamole/");
        dto.SessionId.Should().NotBe(Guid.Empty);
        gateway.LastUserName.Should().Be("unit-test", "o header X-Remote-User é o usuário da plataforma");
        gateway.EnsuredDeviceName.Should().Be(device.Name);
        gateway.EnsuredHostname.Should().Be("192.0.2.90");
        gateway.EnsuredProtocol.Should().Be(RemoteProtocol.Rdp);
        gateway.EnsuredGrantToUser.Should().Be("unit-test");

        var row = await context.RemoteSessions
            .AsNoTracking()
            .SingleAsync(session => session.Id == dto.SessionId);
        row.Status.Should().Be(RemoteSessionStatus.Active);
        row.Protocol.Should().Be(RemoteProtocol.Rdp);
        row.UserId.Should().Be(userId);
        row.DeviceId.Should().Be(device.Id);
    }

    [Fact]
    public async Task Open_LinuxDevice_ShouldPersistSshSession()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(
            context, operatingSystem: DeviceOperatingSystem.Linux);
        var gateway = new FakeGateway();

        var dto = await Handler(context, gateway, new ConfiguredCredentialSource())
            .Handle(new OpenRemoteGatewaySessionCommand(device.Id), CancellationToken.None);

        dto.Protocol.Should().Be("Ssh");
        (await context.RemoteSessions.SingleAsync()).Protocol.Should().Be(RemoteProtocol.Ssh);
    }

    [Fact]
    public async Task Open_UnknownDevice_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => Handler(context, new FakeGateway(), new ConfiguredCredentialSource())
            .Handle(new OpenRemoteGatewaySessionCommand(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Open_DeviceWithoutIp_ShouldThrowArgumentPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(
            context, ip: null, operatingSystem: DeviceOperatingSystem.Windows);

        var action = () => Handler(context, new FakeGateway(), new ConfiguredCredentialSource())
            .Handle(new OpenRemoteGatewaySessionCommand(device.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*não possui endereço IP*");
        (await context.RemoteSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Open_DeviceWithoutOperatingSystem_ShouldThrowArgumentPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context, operatingSystem: null);

        var action = () => Handler(context, new FakeGateway(), new ConfiguredCredentialSource())
            .Handle(new OpenRemoteGatewaySessionCommand(device.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*não possui sistema operacional informado*");
        (await context.RemoteSessions.CountAsync()).Should().Be(
            0, "sem SO não há sessão — o botão nem aparece na ficha");
    }

    [Fact]
    public async Task Open_WhenCredentialNotConfigured_ShouldThrowWithoutMintingOrPersisting()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        var gateway = new FakeGateway();

        var action = () => Handler(context, gateway, new UnconfiguredCredentialSource())
            .Handle(new OpenRemoteGatewaySessionCommand(device.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*não configurada*");
        gateway.LastUserName.Should().BeNull("sem credencial não se minta token");
        (await context.RemoteSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Open_WhenGatewayDown_ShouldThrowConflictAndNotPersistSession()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        var gateway = new FakeGateway(
            "Gateway de acesso remoto indisponível — verifique se o profile 'remote' está de pé.");

        var action = () => Handler(context, gateway, new ConfiguredCredentialSource())
            .Handle(new OpenRemoteGatewaySessionCommand(device.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*profile 'remote'*");
        (await context.RemoteSessions.CountAsync()).Should().Be(
            0, "token não mintado = sem sessão registrada");
    }

    [Fact]
    public async Task Close_GatewaySession_ShouldCloseWithoutRegisteredConnection()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var opened = await Handler(context, new FakeGateway(), new ConfiguredCredentialSource())
            .Handle(new OpenRemoteGatewaySessionCommand(device.Id), CancellationToken.None);

        var closed = await new CloseRemoteSessionCommandHandler(
                new RemoteSessionRepository(context), new RemoteConnectionRegistry())
            .Handle(new CloseRemoteSessionCommand(device.Id, opened.SessionId), CancellationToken.None);

        closed.Status.Should().Be("Closed");
        closed.EndedAt.Should().NotBeNull();
    }

    // ── autorização ──────────────────────────────────────────────────────

    [Fact]
    public void Command_ShouldRequireOperateRole()
    {
        var open = new OpenRemoteGatewaySessionCommand(Guid.NewGuid());

        open.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Operate);
        open.AuditResource.Should().Be("RemoteSession");
    }
}

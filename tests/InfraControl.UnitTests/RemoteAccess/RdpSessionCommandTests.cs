using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
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
/// F11-T003 (ADR-013, docs/10): OpenRdpSessionCommand abre sessão RDP via
/// probe TCP/X.224 — sem autenticação e sem credencial consultada (NLA fica
/// para task futura) — e CloseRemoteSessionCommand encerra e descarta a
/// conexão. Falha de probe persiste sessão Failed auditável.
/// </summary>
public sealed class RdpSessionCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public RdpSessionCommandTests()
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
        AppDbContext context, string? ip = "192.0.2.84")
    {
        var location = Domain.Entities.Location.Create("Sala RDP");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Domain.Entities.Device.Create(
            location.Id, $"ws-rdp-{Guid.NewGuid():N}"[..12], DeviceType.Workstation, ip: ip);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    private static OpenRdpSessionCommandHandler OpenHandler(
        AppDbContext context,
        IRemoteProvider provider,
        IRemoteConnectionRegistry connections,
        Guid? userId = null)
        => new(
            new DeviceRepository(context),
            new RemoteSessionRepository(context),
            new FakeProviderRegistry(provider),
            connections,
            new FakeCurrentUser(userId ?? Guid.NewGuid()));

    private static CloseRemoteSessionCommandHandler CloseHandler(
        AppDbContext context, IRemoteConnectionRegistry connections)
        => new(new RemoteSessionRepository(context), connections);

    // ── fakes ────────────────────────────────────────────────────────────

    private sealed class FakeRdpProvider : IRemoteProvider
    {
        private readonly bool _succeed;
        private readonly string _error;

        public FakeRdpProvider(bool succeed, string? error = null)
        {
            _succeed = succeed;
            _error = error ?? "Endpoint não respondeu como RDP.";
        }

        public FakeConnection? Connection { get; private set; }
        public string? LastHost { get; private set; }
        public string Name => "Rdp";

        public bool CanHandle(string providerKey) =>
            string.Equals(providerKey, "rdp", StringComparison.OrdinalIgnoreCase);

        public Task<RemoteConnectResult> ConnectAsync(
            string host, RemoteCredential credential, CancellationToken cancellationToken = default)
        {
            LastHost = host;
            if (!_succeed)
                return Task.FromResult(new RemoteConnectResult(false, null, _error));

            Connection = new FakeConnection();
            return Task.FromResult(new RemoteConnectResult(true, Connection, null));
        }
    }

    private sealed class FakeConnection : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeProviderRegistry(params IRemoteProvider[] providers)
        : IProviderRegistry<IRemoteProvider>
    {
        public IRemoteProvider Resolve(string providerKey) =>
            providers.FirstOrDefault(provider => provider.CanHandle(providerKey))
            ?? throw new InvalidOperationException(
                $"No provider registered for '{providerKey}'.");

        public IReadOnlyCollection<IRemoteProvider> GetAll() => providers;
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId;
        public string? UserName => "unit-test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => ["Admin"];
        public IReadOnlyCollection<string> Permissions => [];
    }

    // ── OpenRdpSession ───────────────────────────────────────────────────

    [Fact]
    public async Task Open_Success_ShouldCreateActiveSessionAndRegisterConnection()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        var provider = new FakeRdpProvider(succeed: true);
        var connections = new RemoteConnectionRegistry();
        var userId = Guid.NewGuid();

        var dto = await OpenHandler(context, provider, connections, userId)
            .Handle(new OpenRdpSessionCommand(device.Id), CancellationToken.None);

        dto.Protocol.Should().Be("Rdp");
        dto.Status.Should().Be("Active");
        dto.FailureReason.Should().BeNull();
        dto.EndedAt.Should().BeNull();
        dto.UserId.Should().Be(userId);
        provider.LastHost.Should().Be(device.Ip);

        var row = await context.RemoteSessions
            .AsNoTracking()
            .SingleAsync(session => session.Id == dto.Id);
        row.Status.Should().Be(RemoteSessionStatus.Active);
        row.Protocol.Should().Be(RemoteProtocol.Rdp);

        connections.TryDisconnect(dto.Id).Should().BeTrue(
            "a conexão do probe deve ficar registrada na sessão");
    }

    [Fact]
    public async Task Open_UnknownDevice_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => OpenHandler(context, new FakeRdpProvider(true), new RemoteConnectionRegistry())
            .Handle(new OpenRdpSessionCommand(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Open_DeviceWithoutIp_ShouldThrowArgumentPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context, ip: null);

        var action = () => OpenHandler(context, new FakeRdpProvider(true), new RemoteConnectionRegistry())
            .Handle(new OpenRdpSessionCommand(device.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*não possui endereço IP*");
        (await context.RemoteSessions.CountAsync()).Should().Be(
            0, "sem IP não há tentativa de sessão");
    }

    [Fact]
    public async Task Open_WhenProbeFails_ShouldPersistFailedSessionAndThrowConflict()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        var connections = new RemoteConnectionRegistry();

        var action = () => OpenHandler(
                context,
                new FakeRdpProvider(succeed: false, error: "Endpoint não respondeu como RDP."),
                connections)
            .Handle(new OpenRdpSessionCommand(device.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Falha ao abrir sessão RDP*");

        var row = await context.RemoteSessions.AsNoTracking().SingleAsync();
        row.DeviceId.Should().Be(device.Id);
        row.Protocol.Should().Be(RemoteProtocol.Rdp);
        row.Status.Should().Be(RemoteSessionStatus.Failed);
        row.FailureReason.Should().Be("Endpoint não respondeu como RDP.");
        row.EndedAt.Should().NotBeNull();
        connections.TryDisconnect(row.Id).Should().BeFalse(
            "probe que falhou não pode ficar registrado");
    }

    [Fact]
    public async Task Open_WhenErrorIsTooLong_ShouldTruncateFailureReason()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var action = () => OpenHandler(
                context, new FakeRdpProvider(succeed: false, error: new string('x', 600)),
                new RemoteConnectionRegistry())
            .Handle(new OpenRdpSessionCommand(device.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Falha ao abrir sessão RDP*");

        var row = await context.RemoteSessions.AsNoTracking().SingleAsync();
        row.Status.Should().Be(RemoteSessionStatus.Failed);
        row.FailureReason!.Length.Should().Be(RemoteSession.FailureReasonMaxLength);
    }

    // ── CloseRemoteSession ───────────────────────────────────────────────

    [Fact]
    public async Task Close_ActiveRdpSession_ShouldCloseAndDisposeConnection()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        var provider = new FakeRdpProvider(succeed: true);
        var connections = new RemoteConnectionRegistry();

        var opened = await OpenHandler(context, provider, connections)
            .Handle(new OpenRdpSessionCommand(device.Id), CancellationToken.None);

        var dto = await CloseHandler(context, connections)
            .Handle(new CloseRemoteSessionCommand(device.Id, opened.Id), CancellationToken.None);

        dto.Status.Should().Be("Closed");
        dto.EndedAt.Should().NotBeNull();

        var row = await context.RemoteSessions
            .AsNoTracking()
            .SingleAsync(session => session.Id == opened.Id);
        row.Status.Should().Be(RemoteSessionStatus.Closed);
        provider.Connection!.Disposed.Should().BeTrue(
            "encerrar a sessão deve descartar o socket do probe");
        connections.TryDisconnect(opened.Id).Should().BeFalse(
            "a conexão já foi removida do registro");
    }

    // ── autorização ──────────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireRemoteRdpConnectPermission()
    {
        var open = new OpenRdpSessionCommand(Guid.NewGuid());
        var close = new CloseRemoteSessionCommand(Guid.NewGuid(), Guid.NewGuid());

        open.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Operate);
        // close é protocolo-agnóstico: Operator e Admin openede qualquer
        // protocolo (SSH e RDP), ver ADR-016.
        close.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Operate);
    }
}

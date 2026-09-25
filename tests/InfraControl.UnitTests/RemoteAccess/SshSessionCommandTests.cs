using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.RemoteAccess.Sessions;
using InfraControl.Application.RemoteAccess.Sessions.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Enums;
using InfraControl.Domain.RemoteAccess;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.RemoteAccess;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.RemoteAccess;

/// <summary>
/// F11-T002 (ADR-013, docs/10): OpenSshSessionCommand abre sessão SSH via
/// provider (sem terminal interativo) e CloseSshSessionCommand encerra e
/// descarta a conexão. Credenciais vêm de IRemoteCredentialSource (provisório
/// até F11-T006) e nunca aparecem em DTO ou mensagem persistida.
/// </summary>
public sealed class SshSessionCommandTests : IDisposable
{
    private const string SshPassword = "S3nh@Unitária!42";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SshSessionCommandTests()
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
        AppDbContext context, string? ip = "192.0.2.80")
    {
        var location = Domain.Entities.Location.Create("Sala SSH");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Domain.Entities.Device.Create(
            location.Id, $"sw-ssh-{Guid.NewGuid():N}"[..12], DeviceType.Switch, ip: ip);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    private static OpenSshSessionCommandHandler OpenHandler(
        AppDbContext context,
        IRemoteProvider provider,
        IRemoteCredentialSource credentialSource,
        IRemoteConnectionRegistry connections,
        Guid? userId = null)
        => new(
            new DeviceRepository(context),
            new RemoteSessionRepository(context),
            new FakeProviderRegistry(provider),
            credentialSource,
            connections,
            new FakeCurrentUser(userId ?? Guid.NewGuid()));

    private static CloseSshSessionCommandHandler CloseHandler(
        AppDbContext context, IRemoteConnectionRegistry connections)
        => new(new RemoteSessionRepository(context), connections);

    // ── fakes ────────────────────────────────────────────────────────────

    private sealed class FakeRemoteProvider : IRemoteProvider
    {
        private readonly bool _succeed;
        private readonly string _error;

        public FakeRemoteProvider(bool succeed, string? error = null)
        {
            _succeed = succeed;
            _error = error ?? "Host inacessível.";
        }

        public FakeConnection? Connection { get; private set; }
        public string? LastHost { get; private set; }
        public string Name => "SSH";

        public bool CanHandle(string providerKey) =>
            string.Equals(providerKey, "ssh", StringComparison.OrdinalIgnoreCase);

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

    private sealed class ConfiguredCredentialSource : IRemoteCredentialSource
    {
        public Task<RemoteCredential> GetCredentialAsync(
            Guid deviceId, CancellationToken cancellationToken = default)
            => Task.FromResult(new RemoteCredential("it-ssh", SshPassword));
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

    // ── OpenSshSession ───────────────────────────────────────────────────

    [Fact]
    public async Task Open_Success_ShouldCreateActiveSessionAndRegisterConnection()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        var provider = new FakeRemoteProvider(succeed: true);
        var connections = new RemoteConnectionRegistry();
        var userId = Guid.NewGuid();

        var dto = await OpenHandler(
                context, provider, new ConfiguredCredentialSource(), connections, userId)
            .Handle(new OpenSshSessionCommand(device.Id), CancellationToken.None);

        dto.Protocol.Should().Be("Ssh");
        dto.Status.Should().Be("Active");
        dto.FailureReason.Should().BeNull();
        dto.EndedAt.Should().BeNull();
        dto.UserId.Should().Be(userId);
        provider.LastHost.Should().Be(device.Ip);

        var row = await context.RemoteSessions
            .AsNoTracking()
            .SingleAsync(session => session.Id == dto.Id);
        row.Status.Should().Be(RemoteSessionStatus.Active);

        connections.TryDisconnect(dto.Id).Should().BeTrue(
            "a conexão aberta deve ficar registrada na sessão");
    }

    [Fact]
    public async Task Open_UnknownDevice_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => OpenHandler(
                context, new FakeRemoteProvider(true), new ConfiguredCredentialSource(),
                new RemoteConnectionRegistry())
            .Handle(new OpenSshSessionCommand(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Open_DeviceWithoutIp_ShouldThrowArgumentPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context, ip: null);

        var action = () => OpenHandler(
                context, new FakeRemoteProvider(true), new ConfiguredCredentialSource(),
                new RemoteConnectionRegistry())
            .Handle(new OpenSshSessionCommand(device.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*não possui endereço IP*");
        (await context.RemoteSessions.CountAsync()).Should().Be(
            0, "sem IP não há tentativa de sessão");
    }

    [Fact]
    public async Task Open_WhenCredentialNotConfigured_ShouldThrowWithoutPersisting()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var action = () => OpenHandler(
                context, new FakeRemoteProvider(true), new UnconfiguredCredentialSource(),
                new RemoteConnectionRegistry())
            .Handle(new OpenSshSessionCommand(device.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*não configurada*");
        (await context.RemoteSessions.CountAsync()).Should().Be(
            0, "falha de configuração não deve virar sessão");
    }

    [Fact]
    public async Task Open_WhenConnectFails_ShouldPersistFailedSessionAndThrowConflict()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        var connections = new RemoteConnectionRegistry();

        var action = () => OpenHandler(
                context, new FakeRemoteProvider(succeed: false, error: "Connection refused."),
                new ConfiguredCredentialSource(), connections)
            .Handle(new OpenSshSessionCommand(device.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Falha ao abrir sessão SSH*");

        var row = await context.RemoteSessions.AsNoTracking().SingleAsync();
        row.DeviceId.Should().Be(device.Id);
        row.Protocol.Should().Be(RemoteProtocol.Ssh);
        row.Status.Should().Be(RemoteSessionStatus.Failed);
        row.FailureReason.Should().Be("Connection refused.");
        row.EndedAt.Should().NotBeNull();
        connections.TryDisconnect(row.Id).Should().BeFalse(
            "conexão que falhou não pode ficar registrada");
    }

    [Fact]
    public async Task Open_WhenErrorIsTooLong_ShouldTruncateFailureReason()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var action = () => OpenHandler(
                context, new FakeRemoteProvider(succeed: false, error: new string('x', 600)),
                new ConfiguredCredentialSource(), new RemoteConnectionRegistry())
            .Handle(new OpenSshSessionCommand(device.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Falha ao abrir sessão SSH*");

        var row = await context.RemoteSessions.AsNoTracking().SingleAsync();
        row.Status.Should().Be(RemoteSessionStatus.Failed);
        row.FailureReason!.Length.Should().Be(RemoteSession.FailureReasonMaxLength);
    }

    // ── CloseSshSession ──────────────────────────────────────────────────

    [Fact]
    public async Task Close_ActiveSession_ShouldCloseAndDisposeConnection()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        var provider = new FakeRemoteProvider(succeed: true);
        var connections = new RemoteConnectionRegistry();

        var opened = await OpenHandler(
                context, provider, new ConfiguredCredentialSource(), connections)
            .Handle(new OpenSshSessionCommand(device.Id), CancellationToken.None);

        var dto = await CloseHandler(context, connections)
            .Handle(new CloseSshSessionCommand(device.Id, opened.Id), CancellationToken.None);

        dto.Status.Should().Be("Closed");
        dto.EndedAt.Should().NotBeNull();

        var row = await context.RemoteSessions
            .AsNoTracking()
            .SingleAsync(session => session.Id == opened.Id);
        row.Status.Should().Be(RemoteSessionStatus.Closed);
        provider.Connection!.Disposed.Should().BeTrue(
            "encerrar a sessão deve descartar a conexão SSH");
        connections.TryDisconnect(opened.Id).Should().BeFalse(
            "a conexão já foi removida do registro");
    }

    [Fact]
    public async Task Close_UnknownSession_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        var unknownId = Guid.NewGuid();

        var action = () => CloseHandler(context, new RemoteConnectionRegistry())
            .Handle(new CloseSshSessionCommand(device.Id, unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Sessão '{unknownId}' não encontrada.");
    }

    [Fact]
    public async Task Close_SessionOfAnotherDevice_ShouldThrowNotFoundPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var deviceA = await SeedDeviceAsync(context, "192.0.2.81");
        var deviceB = await SeedDeviceAsync(context, "192.0.2.82");
        var connections = new RemoteConnectionRegistry();

        var opened = await OpenHandler(
                context, new FakeRemoteProvider(true), new ConfiguredCredentialSource(), connections)
            .Handle(new OpenSshSessionCommand(deviceA.Id), CancellationToken.None);

        var action = () => CloseHandler(context, connections)
            .Handle(new CloseSshSessionCommand(deviceB.Id, opened.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Sessão '{opened.Id}' não encontrada.");
        var row = await context.RemoteSessions
            .AsNoTracking()
            .SingleAsync(session => session.Id == opened.Id);
        row.Status.Should().Be(RemoteSessionStatus.Active,
            "a sessão pertence a outro device e não pode ser fechada por aqui");
    }

    [Fact]
    public async Task Close_AlreadyFinalizedSession_ShouldThrowConflictPtBrMessage()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        var connections = new RemoteConnectionRegistry();

        var opened = await OpenHandler(
                context, new FakeRemoteProvider(true), new ConfiguredCredentialSource(), connections)
            .Handle(new OpenSshSessionCommand(device.Id), CancellationToken.None);

        await CloseHandler(context, connections)
            .Handle(new CloseSshSessionCommand(device.Id, opened.Id), CancellationToken.None);

        var action = () => CloseHandler(context, connections)
            .Handle(new CloseSshSessionCommand(device.Id, opened.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*já foi finalizada*");
    }

    // ── autorização ──────────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireRemoteSshConnectPermission()
    {
        var open = new OpenSshSessionCommand(Guid.NewGuid());
        var close = new CloseSshSessionCommand(Guid.NewGuid(), Guid.NewGuid());

        open.RequiredPermissions.Should()
            .Equal(Permission.RemoteSshConnect.ToString());
        close.RequiredPermissions.Should()
            .Equal(Permission.RemoteSshConnect.ToString());
    }
}

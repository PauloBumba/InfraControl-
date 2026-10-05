using System.Reflection;
using FluentAssertions;
using InfraControl.Application.RemoteAccess.Sessions;
using InfraControl.Domain.Enums;
using InfraControl.Domain.RemoteAccess;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.RemoteAccess;

/// <summary>
/// F11-T001: GetDeviceSessionsQuery — histórico de sessões do device
/// (permissão RemoteSessionsRead, ordem mais-recente-primeiro, DTO sem
/// credenciais — docs/10 "Nunca expor credenciais ao frontend").
/// </summary>
public sealed class DeviceSessionsQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public DeviceSessionsQueryTests()
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

    private static GetDeviceSessionsQueryHandler Handler(AppDbContext context)
        => new(new DeviceRepository(context), new RemoteSessionRepository(context));

    private static async Task<Domain.Entities.Device> SeedDeviceAsync(AppDbContext context)
    {
        var location = Domain.Entities.Location.Create("Sala Remota");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Domain.Entities.Device.Create(
            location.Id, $"rm-{Guid.NewGuid():N}"[..10], DeviceType.Switch);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    private static async Task SeedSessionsAsync(
        AppDbContext context, Domain.Entities.Device device, int count)
    {
        var start = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < count; i++)
        {
            var session = RemoteSession.Open(
                device.Id,
                Guid.NewGuid(),
                i % 2 == 0 ? RemoteProtocol.Ssh : RemoteProtocol.Rdp);
            context.RemoteSessions.Add(session);
            await context.SaveChangesAsync();

            // SaveChangesAsync sobrescreve CreateDateTime (auditoria); a
            // ordenação usa StartedAt, então ajusta datas distintas via EF.
            var entry = context.Entry(session);
            entry.Property(nameof(RemoteSession.StartedAt)).CurrentValue =
                start.AddMinutes(i);
            entry.State = EntityState.Modified;
            await context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetDeviceSessions_ShouldReturnSessionsNewestFirst()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        await SeedSessionsAsync(context, device, count: 3);

        var dto = await Handler(context).Handle(
            new GetDeviceSessionsQuery(device.Id), CancellationToken.None);

        dto.DeviceId.Should().Be(device.Id);
        dto.DeviceName.Should().Be(device.Name);
        dto.Sessions.Should().HaveCount(3);
        dto.Sessions.Select(session => session.StartedAt)
            .Should().BeInDescendingOrder("sessões mais recentes primeiro");
        dto.Sessions.Select(session => session.Protocol)
            .Should().BeEquivalentTo(["Ssh", "Rdp", "Ssh"]);
    }

    [Fact]
    public async Task GetDeviceSessions_UnknownDevice_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => Handler(context).Handle(
            new GetDeviceSessionsQuery(Guid.NewGuid()), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("*não encontrado*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task GetDeviceSessions_InvalidLimit_ShouldThrowPtBr(int limit)
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var action = () => Handler(context).Handle(
            new GetDeviceSessionsQuery(device.Id, limit), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*limite*");
    }

    [Fact]
    public async Task GetDeviceSessions_NoSessions_ShouldReturnEmptyList()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var dto = await Handler(context).Handle(
            new GetDeviceSessionsQuery(device.Id), CancellationToken.None);

        dto.Sessions.Should().BeEmpty();
    }

    [Fact]
    public void RemoteSessionDto_ShouldNotExposeCredentialProperties()
    {
        // docs/10: "Nunca expor credenciais ao frontend."
        var forbidden = new[] { "password", "secret", "credential", "token", "passphrase", "community" };

        var properties = typeof(RemoteSessionDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        properties.Should().NotBeEmpty();
        properties.Should().NotContain(
            property => forbidden.Any(word =>
                property.Name.Contains(word, StringComparison.OrdinalIgnoreCase)),
            "o DTO de sessão não pode transportar credenciais");
    }

    // ADR-030 §2 — GET .../remote/sessions/{sessionId}: recuperação pontual
    // da sessão DO device (fora do device = 404, mesma resposta do inexistente).

    [Fact]
    public async Task GetRemoteSession_KnownSession_ShouldReturnDto()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        var session = RemoteSession.Open(device.Id, Guid.NewGuid(), RemoteProtocol.Ssh);
        context.RemoteSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new GetRemoteSessionQueryHandler(new RemoteSessionRepository(context));
        var dto = await handler.Handle(
            new GetRemoteSessionQuery(device.Id, session.Id), CancellationToken.None);

        dto.Id.Should().Be(session.Id);
        dto.Protocol.Should().Be(nameof(RemoteProtocol.Ssh));
        dto.Status.Should().Be(nameof(RemoteSessionStatus.Active));
    }

    [Fact]
    public async Task GetRemoteSession_FromAnotherDevice_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var deviceA = await SeedDeviceAsync(context);
        var deviceB = await SeedDeviceAsync(context);
        var session = RemoteSession.Open(deviceA.Id, Guid.NewGuid(), RemoteProtocol.Rdp);
        context.RemoteSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new GetRemoteSessionQueryHandler(new RemoteSessionRepository(context));
        var action = () => handler.Handle(
            new GetRemoteSessionQuery(deviceB.Id, session.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Sessão de acesso remoto '{session.Id}' não encontrada.");
    }
}

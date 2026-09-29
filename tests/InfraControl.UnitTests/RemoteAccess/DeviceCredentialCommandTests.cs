using System.Reflection;
using FluentAssertions;
using InfraControl.Application.RemoteAccess.Credentials;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.Domain.RemoteAccess;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.RemoteAccess;

/// <summary>
/// F11-T006: gestão de credencial por device — upsert/rotação com audit de
/// RotatedAt, 404 pt-BR, permissão CredentialManage e DTO sem senha
/// (docs/10 "Nunca expor credenciais ao frontend").
/// </summary>
public sealed class DeviceCredentialCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly FakeProtector _protector = new();

    public DeviceCredentialCommandTests()
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

    private UpsertDeviceCredentialCommandHandler UpsertHandler(AppDbContext context)
        => new(new DeviceRepository(context),
            new DeviceCredentialRepository(context), _protector);

    private DeleteDeviceCredentialCommandHandler DeleteHandler(AppDbContext context)
        => new(new DeviceRepository(context), new DeviceCredentialRepository(context));

    private GetDeviceCredentialQueryHandler QueryHandler(AppDbContext context)
        => new(new DeviceRepository(context), new DeviceCredentialRepository(context));

    private static async Task<Domain.Entities.Device> SeedDeviceAsync(AppDbContext context)
    {
        var location = Domain.Entities.Location.Create("Sala Credencial");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Domain.Entities.Device.Create(
            location.Id, $"rc-{Guid.NewGuid():N}"[..10], DeviceType.Switch);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    [Fact]
    public async Task Upsert_NewCredential_ShouldPersistWithoutRotatedAt()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var dto = await UpsertHandler(context).Handle(
            new UpsertDeviceCredentialCommand(device.Id, "svc-backup", "SenhaForte!1"),
            CancellationToken.None);

        dto.DeviceId.Should().Be(device.Id);
        dto.Username.Should().Be("svc-backup");
        dto.HasCredential.Should().BeTrue();
        dto.RotatedAt.Should().BeNull("a primeira gravação não é rotação");

        var stored = await context.DeviceCredentials
            .SingleAsync(credential => credential.DeviceId == device.Id);
        stored.ProtectedPassword.Should().NotContain("SenhaForte!1",
            "a senha nunca é gravada em texto puro");
    }

    [Fact]
    public async Task Upsert_ExistingCredential_ShouldRotateAndStampRotatedAt()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        await UpsertHandler(context).Handle(
            new UpsertDeviceCredentialCommand(device.Id, "svc-antigo", "SenhaAntiga!1"),
            CancellationToken.None);

        var dto = await UpsertHandler(context).Handle(
            new UpsertDeviceCredentialCommand(device.Id, "svc-novo", "SenhaNova!2"),
            CancellationToken.None);

        dto.Username.Should().Be("svc-novo");
        dto.RotatedAt.Should().NotBeNull("substituir credencial é rotação");
        (await context.DeviceCredentials.CountAsync())
            .Should().Be(1, "upsert não duplica a credencial do device");
    }

    [Fact]
    public async Task Upsert_UnknownDevice_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => UpsertHandler(context).Handle(
            new UpsertDeviceCredentialCommand(unknownId, "svc", "Senha!1"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }

    [Theory]
    [InlineData("", "Senha!1", "*usuário*")]
    [InlineData("   ", "Senha!1", "*usuário*")]
    [InlineData("svc", "", "*senha*")]
    [InlineData("svc", "   ", "*senha*")]
    public async Task Upsert_InvalidInput_ShouldThrowPtBr(
        string username, string password, string message)
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var action = () => UpsertHandler(context).Handle(
            new UpsertDeviceCredentialCommand(device.Id, username, password),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage(message);
        (await context.DeviceCredentials.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Upsert_UsernameLongerThanMax_ShouldThrowPtBr()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var action = () => UpsertHandler(context).Handle(
            new UpsertDeviceCredentialCommand(
                device.Id, new string('u', DeviceCredential.UsernameMaxLength + 1), "Senha!1"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*máximo 256*");
    }

    [Fact]
    public async Task Delete_ExistingCredential_ShouldRemoveIt()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        await UpsertHandler(context).Handle(
            new UpsertDeviceCredentialCommand(device.Id, "svc", "Senha!1"),
            CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteDeviceCredentialCommand(device.Id), CancellationToken.None);

        (await context.DeviceCredentials.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Delete_UnknownDevice_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => DeleteHandler(context).Handle(
            new DeleteDeviceCredentialCommand(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Delete_DeviceWithoutCredential_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var action = () => DeleteHandler(context).Handle(
            new DeleteDeviceCredentialCommand(device.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("*não encontrada*");
    }

    [Fact]
    public async Task Get_WithCredential_ShouldReturnUsernameWithoutPassword()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);
        await UpsertHandler(context).Handle(
            new UpsertDeviceCredentialCommand(device.Id, "svc-backup", "SenhaForte!1"),
            CancellationToken.None);

        var dto = await QueryHandler(context).Handle(
            new GetDeviceCredentialQuery(device.Id), CancellationToken.None);

        dto.Username.Should().Be("svc-backup");
        dto.HasCredential.Should().BeTrue();
        dto.DeviceId.Should().Be(device.Id);
    }

    [Fact]
    public async Task Get_WithoutCredential_ShouldReturnHasCredentialFalse()
    {
        await using var context = await CreateContextAsync();
        var device = await SeedDeviceAsync(context);

        var dto = await QueryHandler(context).Handle(
            new GetDeviceCredentialQuery(device.Id), CancellationToken.None);

        dto.HasCredential.Should().BeFalse();
        dto.Username.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_UnknownDevice_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => QueryHandler(context).Handle(
            new GetDeviceCredentialQuery(Guid.NewGuid()), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("*não encontrado*");
    }

    [Fact]
    public void Commands_ShouldRequireCredentialManageAndAuditAsDeviceCredential()
    {
        var required = RoleAccess.Administer;

        new UpsertDeviceCredentialCommand(Guid.NewGuid(), "svc", "Senha!1")
            .RequiredRoles.Should().BeEquivalentTo(required);
        new DeleteDeviceCredentialCommand(Guid.NewGuid())
            .RequiredRoles.Should().BeEquivalentTo(required);
        new GetDeviceCredentialQuery(Guid.NewGuid())
            .RequiredRoles.Should().BeEquivalentTo(required);

        ((IAuditableRequest)new UpsertDeviceCredentialCommand(Guid.NewGuid(), "svc", "Senha!1"))
            .AuditResource.Should().Be("DeviceCredential");
        ((IAuditableRequest)new DeleteDeviceCredentialCommand(Guid.NewGuid()))
            .AuditResource.Should().Be("DeviceCredential");
    }

    [Fact]
    public void DeviceCredentialDto_ShouldNotExposePasswordProperties()
    {
        // docs/10: "Nunca expor credenciais ao frontend."
        var forbidden = new[] { "password", "senha", "secret", "token", "passphrase", "protected" };

        var properties = typeof(DeviceCredentialDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        properties.Should().NotBeEmpty();
        properties.Should().NotContain(
            property => forbidden.Any(word =>
                property.Name.Contains(word, StringComparison.OrdinalIgnoreCase)),
            "o DTO de credencial não pode transportar a senha");
        typeof(DeviceCredentialDto).GetProperty("HasCredential")!.PropertyType
            .Should().Be(typeof(bool));
    }

    private sealed class FakeProtector : ICredentialProtector
    {
        public string Protect(string plainText)
            => $"v1:{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainText))}";

        public string Unprotect(string protectedText)
            => protectedText.StartsWith("v1:", StringComparison.Ordinal)
                ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(
                    protectedText["v1:".Length..]))
                : throw new InvalidOperationException("Falha ao decifrar credencial armazenada.");
    }
}

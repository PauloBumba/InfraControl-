using FluentAssertions;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Application.RemoteAccess.Sessions.Ports;
using InfraControl.Domain.RemoteAccess;
using InfraControl.Infrastructure.RemoteAccess;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.RemoteAccess;

/// <summary>
/// F11-T006: cadeia de credencial — store por device (cifrado) primeiro,
/// fallback global RemoteAccess:Ssh depois, erro pt-BR se nada existir.
/// </summary>
public sealed class StoreRemoteCredentialSourceTests
{
    private const string ValidKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    [Fact]
    public async Task GetCredential_StoreHit_ShouldReturnDecryptedCredential()
    {
        var deviceId = Guid.NewGuid();
        var protector = new AesGcmCredentialProtector(Options.Create(
            new CredentialEncryptionOptions { Key = ValidKey }));
        var repository = new FakeDeviceCredentialRepository(
            DeviceCredential.Set(deviceId, "svc-store", protector.Protect("SenhaDoStore!1")));
        var source = new StoreRemoteCredentialSource(
            repository, protector, Fallback(Options.Create(new RemoteSshOptions())));

        var credential = await source.GetCredentialAsync(deviceId);

        credential.Username.Should().Be("svc-store");
        credential.Password.Should().Be("SenhaDoStore!1");
    }

    [Fact]
    public async Task GetCredential_StoreMiss_ShouldFallBackToGlobalConfig()
    {
        var protector = new AesGcmCredentialProtector(Options.Create(
            new CredentialEncryptionOptions { Key = ValidKey }));
        var source = new StoreRemoteCredentialSource(
            new FakeDeviceCredentialRepository(null), protector,
            Fallback(Options.Create(new RemoteSshOptions
            {
                Username = "global-user",
                Password = "global-pass"
            })));

        var credential = await source.GetCredentialAsync(Guid.NewGuid());

        credential.Username.Should().Be("global-user");
        credential.Password.Should().Be("global-pass");
    }

    [Fact]
    public async Task GetCredential_StoreMissAndNoConfig_ShouldThrowPtBr()
    {
        var protector = new AesGcmCredentialProtector(Options.Create(
            new CredentialEncryptionOptions { Key = ValidKey }));
        var source = new StoreRemoteCredentialSource(
            new FakeDeviceCredentialRepository(null), protector,
            Fallback(Options.Create(new RemoteSshOptions())));

        var action = () => source.GetCredentialAsync(Guid.NewGuid());

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*não configurada*");
    }

    private static ConfigurationRemoteCredentialSource Fallback(
        Microsoft.Extensions.Options.IOptions<RemoteSshOptions> options)
        => new(options);

    private sealed class FakeDeviceCredentialRepository(DeviceCredential? credential)
        : IDeviceCredentialRepository
    {
        public Task<DeviceCredential?> GetByDeviceIdAsync(
            Guid deviceId, CancellationToken cancellationToken = default)
            => Task.FromResult(credential);

        public Task<DeviceCredential?> GetByIdAsync(
            Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(credential);

        public Task AddAsync(DeviceCredential entity, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Remove(DeviceCredential entity) { }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}

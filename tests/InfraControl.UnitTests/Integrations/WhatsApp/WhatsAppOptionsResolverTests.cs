using FluentAssertions;
using InfraControl.Application.Integrations.WhatsApp.Ports;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Integrations.WhatsApp;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InfraControl.UnitTests.Integrations.WhatsApp;

public sealed class WhatsAppOptionsResolverTests
{
    private readonly Mock<IWhatsAppSettingsRepository> _repository = new();
    private readonly Mock<IWhatsAppEnvOptionsAccessor> _envAccessor = new();
    private readonly Mock<ICredentialProtector> _protector = new();

    private WhatsAppOptionsResolver CreateResolver()
        => new(
            _repository.Object,
            _envAccessor.Object,
            _protector.Object,
            NullLogger<WhatsAppOptionsResolver>.Instance);

    [Fact]
    public async Task ResolveAsync_WhenDatabaseRecordExists_ShouldReturnDatabaseOptionsWithDecryptedKey()
    {
        var setting = WhatsAppIntegrationSetting.Create(
            "http://db-evolution:8080",
            "encrypted-secret",
            "DbInstance",
            "db-group@g.us",
            "5511999999999");

        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);
        _protector.Setup(p => p.Unprotect("encrypted-secret"))
            .Returns("decrypted-plain-key");

        var resolver = CreateResolver();
        var options = await resolver.ResolveAsync(CancellationToken.None);

        options.BaseUrl.Should().Be("http://db-evolution:8080");
        options.InstanceId.Should().Be("DbInstance");
        options.ApiKey.Should().Be("decrypted-plain-key");
        options.GroupId.Should().Be("db-group@g.us");
        options.Phone.Should().Be("5511999999999");
    }

    [Fact]
    public async Task ResolveAsync_WhenDatabaseRecordMissing_ShouldFallbackToEnvironmentOptions()
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppIntegrationSetting?)null);

        var envOptions = new WhatsAppChannelOptions
        {
            BaseUrl = "http://env-evolution:8080",
            ApiKey = "env-key",
            InstanceId = "EnvInstance",
            GroupId = "env-group@g.us"
        };
        _envAccessor.Setup(e => e.Current).Returns(envOptions);

        var resolver = CreateResolver();
        var options = await resolver.ResolveAsync(CancellationToken.None);

        options.Should().BeSameAs(envOptions);
    }

    [Fact]
    public async Task ResolveAsync_WhenUnprotectThrows_ShouldQuietlyReturnEmptyKeyWithoutThrowing()
    {
        var setting = WhatsAppIntegrationSetting.Create(
            "http://db-evolution:8080",
            "corrupted-key",
            "DbInstance");

        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);
        _protector.Setup(p => p.Unprotect("corrupted-key"))
            .Throws(new InvalidOperationException("Decryption failure"));

        var resolver = CreateResolver();
        var options = await resolver.ResolveAsync(CancellationToken.None);

        options.ApiKey.Should().BeEmpty();
        options.BaseUrl.Should().Be("http://db-evolution:8080");
    }
}

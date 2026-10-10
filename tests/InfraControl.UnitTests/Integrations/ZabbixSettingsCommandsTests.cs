using FluentAssertions;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F27 Fase 2.2: configuração administrativa do Zabbix — senha write-only
/// e cifrada (omitação mantém a gravada), fallback banco → env e resultado
/// do teste de conexão persistido sem quebrar por falha de rede.
/// </summary>
public sealed class ZabbixSettingsCommandsTests
{
    [Fact]
    public async Task GetSettings_WithoutRow_ShouldFallBackToEnvironment()
    {
        var repository = new Mock<IZabbixSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ZabbixIntegrationSetting?)null);
        var accessor = new Mock<IZabbixApiOptionsAccessor>();
        accessor.SetupGet(a => a.Current).Returns(new ZabbixIntegrationOptions
        {
            ApiUrl = "http://zabbix-env:8080",
            Username = "env-user",
            Password = "env-senha"
        });

        var handler = new GetZabbixSettingsQueryHandler(repository.Object, accessor.Object);
        var dto = await handler.Handle(new GetZabbixSettingsQuery(), CancellationToken.None);

        dto.Source.Should().Be("environment");
        dto.ApiUrl.Should().Be("http://zabbix-env:8080");
        dto.PasswordSet.Should().BeTrue();
        dto.DetectedVersion.Should().BeNull();
    }

    [Fact]
    public async Task GetSettings_WithRow_ShouldReturnDatabaseSourceWithoutPassword()
    {
        var setting = ZabbixIntegrationSetting.Create(
            "http://zabbix-banco:8080", "admin", "enc-abc");
        setting.RecordTestResult(
            ok: true, "8.0.0", DateTime.UtcNow, error: null);
        var repository = new Mock<IZabbixSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);
        var accessor = new Mock<IZabbixApiOptionsAccessor>();

        var handler = new GetZabbixSettingsQueryHandler(repository.Object, accessor.Object);
        var dto = await handler.Handle(new GetZabbixSettingsQuery(), CancellationToken.None);

        dto.Source.Should().Be("database");
        dto.PasswordSet.Should().BeTrue();
        dto.DetectedVersion.Should().Be("8.0.0");
        dto.LastTestOk.Should().BeTrue();
        dto.LastTestError.Should().BeNull();
    }

    [Fact]
    public async Task SaveSettings_WithPassword_ShouldEncryptAndPersist()
    {
        var repository = new Mock<IZabbixSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ZabbixIntegrationSetting?)null);
        repository.Setup(r => r.SaveAsync(It.IsAny<ZabbixIntegrationSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var protector = new Mock<ICredentialProtector>();
        protector.Setup(p => p.Protect("plain-senha")).Returns("enc-senha");

        var handler = new SaveZabbixSettingsCommandHandler(repository.Object, protector.Object);
        var dto = await handler.Handle(
            new SaveZabbixSettingsCommand(new SaveZabbixSettingsRequest(
                "http://zabbix.exemplo.test:8080", "Admin", "plain-senha")),
            CancellationToken.None);

        dto.Source.Should().Be("database");
        dto.PasswordSet.Should().BeTrue();
        repository.Verify(r => r.SaveAsync(
            It.Is<ZabbixIntegrationSetting>(s =>
                s.ApiUrl == "http://zabbix.exemplo.test:8080" &&
                s.Username == "Admin" &&
                s.PasswordEncrypted == "enc-senha"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveSettings_WithoutPassword_ShouldKeepExistingEncryptedPassword()
    {
        var existing = ZabbixIntegrationSetting.Create(
            "http://antigo:8080", "admin", "enc-antiga");
        var repository = new Mock<IZabbixSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var protector = new Mock<ICredentialProtector>();

        var handler = new SaveZabbixSettingsCommandHandler(repository.Object, protector.Object);
        var dto = await handler.Handle(
            new SaveZabbixSettingsCommand(new SaveZabbixSettingsRequest(
                "http://novo:8080", "Admin", Password: null)),
            CancellationToken.None);

        dto.PasswordSet.Should().BeTrue();
        protector.Verify(p => p.Protect(It.IsAny<string>()), Times.Never);
        existing.ApiUrl.Should().Be("http://novo:8080");
        existing.PasswordEncrypted.Should().Be("enc-antiga");
    }

    [Fact]
    public async Task SaveSettings_WithoutPasswordAndNoExisting_ShouldThrow()
    {
        var repository = new Mock<IZabbixSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ZabbixIntegrationSetting?)null);
        var protector = new Mock<ICredentialProtector>();

        var handler = new SaveZabbixSettingsCommandHandler(repository.Object, protector.Object);
        var act = async () => await handler.Handle(
            new SaveZabbixSettingsCommand(new SaveZabbixSettingsRequest(
                "http://zabbix:8080", "Admin", Password: null)),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*obrigatória*");
    }

    [Fact]
    public async Task TestConnection_WhenUnconfigured_ShouldFailFastWithoutClientCall()
    {
        var resolver = new Mock<IZabbixOptionsResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZabbixIntegrationOptions());
        var client = new Mock<IZabbixClient>();

        var handler = new TestZabbixConnectionCommandHandler(
            resolver.Object,
            Mock.Of<IZabbixSettingsRepository>(),
            client.Object,
            NullLogger<TestZabbixConnectionCommandHandler>.Instance);

        var result = await handler.Handle(
            new TestZabbixConnectionCommand(), CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("Configuração incompleta");
        client.Verify(c => c.GetVersionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TestConnection_OnSuccess_ShouldPersistVersionOnSettingRow()
    {
        var setting = ZabbixIntegrationSetting.Create(
            "http://zabbix:8080", "admin", "enc");
        var resolver = new Mock<IZabbixOptionsResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZabbixIntegrationOptions
            {
                ApiUrl = "http://zabbix:8080",
                Username = "admin",
                Password = "plain"
            });
        var repository = new Mock<IZabbixSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);
        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("8.0.0");

        var handler = new TestZabbixConnectionCommandHandler(
            resolver.Object, repository.Object, client.Object,
            NullLogger<TestZabbixConnectionCommandHandler>.Instance);

        var result = await handler.Handle(
            new TestZabbixConnectionCommand(), CancellationToken.None);

        result.Ok.Should().BeTrue();
        result.Version.Should().Be("8.0.0");
        setting.DetectedVersion.Should().Be("8.0.0");
        setting.LastTestOk.Should().BeTrue();
        setting.LastCommunicationAtUtc.Should().NotBeNull();
        repository.Verify(r => r.SaveAsync(setting, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TestConnection_OnFailure_ShouldRecordErrorAndNotThrow()
    {
        var setting = ZabbixIntegrationSetting.Create(
            "http://zabbix:8080", "admin", "enc");
        var resolver = new Mock<IZabbixOptionsResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZabbixIntegrationOptions
            {
                ApiUrl = "http://zabbix:8080",
                Username = "admin",
                Password = "plain"
            });
        var repository = new Mock<IZabbixSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);
        var client = new Mock<IZabbixClient>();
        client.Setup(c => c.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("zabbix fora do ar"));

        var handler = new TestZabbixConnectionCommandHandler(
            resolver.Object, repository.Object, client.Object,
            NullLogger<TestZabbixConnectionCommandHandler>.Instance);

        var result = await handler.Handle(
            new TestZabbixConnectionCommand(), CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("zabbix fora do ar");
        setting.LastTestOk.Should().BeFalse();
        setting.LastTestError.Should().Contain("zabbix fora do ar");
    }

    [Fact]
    public void Setting_Update_ShouldRejectInvalidUrl()
    {
        var act = () => ZabbixIntegrationSetting.Create("sem-esquema", "admin", "enc");

        act.Should().Throw<ArgumentException>().WithMessage("*absoluta*");
    }

    [Fact]
    public void Setting_RecordTestResult_ShouldTruncateLongError()
    {
        var setting = ZabbixIntegrationSetting.Create(
            "http://zabbix:8080", "admin", "enc");

        setting.RecordTestResult(
            ok: false, null, DateTime.UtcNow, new string('x', 2000));

        setting.LastTestError!.Length.Should().Be(ZabbixIntegrationSetting.MaxErrorLength);
        setting.LastTestOk.Should().BeFalse();
        setting.DetectedVersion.Should().BeNull();
    }
}
